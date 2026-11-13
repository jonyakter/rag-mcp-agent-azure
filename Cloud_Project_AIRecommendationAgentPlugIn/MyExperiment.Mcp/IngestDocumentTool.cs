// Uses RecommendationService (injected) for the embedding service + SQL repository, and a
// BlobContainerClient against the EXISTING training-files container (McpConfig.TrainingContainer) —
// no new Blob container is created for this tool.

using AIRecommendation.Application.Services;
using AIRecommendation.Domain.Entities;
using AIRecommendation.Infrastructure.Parsing;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyExperiment.Mcp
{
    /// <summary>
    /// MCP tool that ingests a dataset file already uploaded to the training-files Blob
    /// container: downloads it, extracts its text, generates its embedding, and stores it as a
    /// new row in the Azure SQL document store (s4.Documents) so it becomes searchable by
    /// recommend / find_similar_documents. Only .txt and .csv are supported.
    /// </summary>
    [McpServerToolType]
    public sealed class IngestDocumentTools
    {
        private readonly RecommendationService _service;
        private readonly McpConfig _config;

        public IngestDocumentTools(RecommendationService service, IOptions<McpConfig> config)
        {
            _service = service;
            _config = config.Value;
        }

        [McpServerTool, Description(
            "Ingests a dataset file that has already been uploaded to the training-files Blob " +
            "container: downloads it, generates embeddings, and stores it in the Azure SQL " +
            "document store so it becomes searchable via recommend / find_similar_documents. " +
            "For a .csv with multiple rows (e.g. one row per book/movie), each row becomes its " +
            "own separate, individually-searchable document. A .txt file becomes a single " +
            "document. Use this when the user wants to add, upload, or import a new dataset.")]
        public async Task<string> IngestDocument(
            [Description("Name of the blob in the training-files container, e.g. 'new-movies.csv'")]
            string blobName,
            [Description("Optional category, e.g. 'movies', 'books', 'restaurant'. For CSV rows, a " +
                          "Genre/Category column in the row is used instead when this is empty.")]
            string category = "",
            [Description("Optional display title. Only used for .txt files — defaults to the blob name.")]
            string title = "")
        {
            if (string.IsNullOrWhiteSpace(blobName))
                return "Please provide the blob name of the dataset to ingest.";

            string extension = Path.GetExtension(blobName).ToLowerInvariant();
            if (extension != ".txt" && extension != ".csv")
                return $"Unsupported file type '{extension}'. Only .txt and .csv are supported for ingestion.";

            var container = new BlobContainerClient(_config.StorageConnectionString, _config.TrainingContainer);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlobClient(blobName);
            if (!await blob.ExistsAsync())
                return $"Blob '{blobName}' was not found in container '{_config.TrainingContainer}'. Upload it first.";

            string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            try
            {
                await blob.DownloadToAsync(tempPath);

                return extension == ".csv"
                    ? await IngestCsvRowsAsync(tempPath, blobName, category)
                    : await IngestSingleTextFileAsync(tempPath, blobName, category, title);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        /// <summary>One document per CSV row — mirrors how the existing movie dataset is stored.</summary>
        private async Task<string> IngestCsvRowsAsync(string tempPath, string blobName, string category)
        {
            var rows = CsvRowsReader.ReadRows(tempPath).ToList();
            if (rows.Count == 0)
                return $"No rows found in '{blobName}'.";

            // Idempotent: re-ingesting the same file replaces its old rows instead of
            // appending duplicates alongside them (also guards against a client-side retry
            // of this same call resulting in the file being ingested twice).
            await _service.Repository.DeleteByFileNameAsync(blobName);

            var titles = new List<string>();
            foreach (var row in rows)
            {
                string rowTitle = FindColumn(row, "name") ?? FindColumn(row, "title")
                    ?? row.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? blobName;
                string rowCategory = !string.IsNullOrWhiteSpace(category)
                    ? category
                    : FindColumn(row, "genre") ?? FindColumn(row, "category") ?? string.Empty;
                string content = string.Join("\n", row.Select(kv => $"{kv.Key}: {kv.Value}"));

                float[] vector = _service.EmbeddingService.GenerateEmbedding(content);

                await _service.Repository.AddAsync(new DocumentRecord
                {
                    FileName = blobName,
                    FileType = "csv",
                    FilePath = $"{_config.TrainingContainer}/{blobName}",
                    Title = rowTitle,
                    Content = content,
                    EmbeddingJson = JsonSerializer.Serialize(vector),
                    Category = rowCategory,
                    ImportedAt = DateTime.UtcNow
                });
                titles.Add(rowTitle);
            }

            await _service.Repository.SaveChangesAsync();

            string sample = string.Join(", ", titles.Take(5)) + (titles.Count > 5 ? ", ..." : "");
            return $"Ingested {rows.Count} row(s) from '{blobName}' as {rows.Count} separate documents. " +
                   $"Examples: {sample}. Now searchable via recommend / find_similar_documents.";
        }

        /// <summary>Whole file as a single document — used for .txt files.</summary>
        private async Task<string> IngestSingleTextFileAsync(
            string tempPath, string blobName, string category, string title)
        {
            string text = await new TxtDocumentParser().ExtractTextAsync(tempPath);
            if (string.IsNullOrWhiteSpace(text))
                return $"No text could be extracted from '{blobName}'.";

            // Idempotent: re-ingesting the same file replaces the old document instead of
            // creating a duplicate alongside it.
            await _service.Repository.DeleteByFileNameAsync(blobName);

            float[] vector = _service.EmbeddingService.GenerateEmbedding(text);

            var doc = new DocumentRecord
            {
                FileName = blobName,
                FileType = "txt",
                FilePath = $"{_config.TrainingContainer}/{blobName}",
                Title = string.IsNullOrWhiteSpace(title) ? blobName : title,
                Content = text,
                EmbeddingJson = JsonSerializer.Serialize(vector),
                Category = category ?? string.Empty,
                ImportedAt = DateTime.UtcNow
            };

            await _service.Repository.AddAsync(doc);
            await _service.Repository.SaveChangesAsync();

            return $"Ingested '{blobName}' as document {doc.Id} " +
                   $"(title: \"{doc.Title}\", category: \"{doc.Category}\", " +
                   $"{text.Length} chars, {vector.Length}-dim embedding). " +
                   "It is now searchable via recommend / find_similar_documents.";
        }

        private static string? FindColumn(Dictionary<string, string> row, string keyword)
        {
            foreach (var kv in row)
                if (kv.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value;
            return null;
        }
    }
}
