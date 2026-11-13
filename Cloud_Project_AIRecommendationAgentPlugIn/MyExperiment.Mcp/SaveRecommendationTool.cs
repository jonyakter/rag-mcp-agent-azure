// Uses RecommendationService (injected) for the shared ranking logic, a BlobContainerClient for
// Azure Blob Storage, and a TableClient for Azure Table Storage — same packages/versions MyExperiment
// already uses in AzureBlobStorageProvider.cs / AzureTableStorage.cs. Both live on the same Storage
// Account, so one connection string (McpConfig.StorageConnectionString) covers both.

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MyExperiment.Mcp
{
    /// <summary>
    /// MCP tool that runs a recommendation query and saves the result to Blob Storage (JSON file)
    /// and Table Storage (summary row). Reuses RecommendationService.BuildRecommendationAsync — the
    /// same ranking logic RecommendationTools.Recommend uses — so saved results always match what
    /// Recommend returns.
    /// </summary>
    [McpServerToolType]
    public sealed class SaveRecommendationTools
    {
        private readonly RecommendationService _service;
        private readonly McpConfig _config;

        public SaveRecommendationTools(RecommendationService service, IOptions<McpConfig> config)
        {
            _service = service;
            _config = config.Value;
        }

        [McpServerTool, Description(
            "Runs a recommendation query and saves the result as a JSON file to Blob Storage, plus a " +
            "summary row in Table Storage. Use this when the user wants to keep, save, or export a " +
            "recommendation — not just see it.")]
        public async Task<string> SaveRecommendation(
            [Description("Natural-language query, e.g. 'feel-good sci-fi from the 1980s'")]
            string query,
            [Description("Optional category: 'movies', 'books', 'restaurant'. Empty = all.")]
            string category = "")
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Please provide a query to save a recommendation for.";

            var startUtc = DateTime.UtcNow;
            var response = await _service.BuildRecommendationAsync(query, category);

            // ── Blob: full JSON result ────────────────────────────────────────────────
            string shortGuid = Guid.NewGuid().ToString("N")[..8];
            string blobName = $"mcp-{DateTime.UtcNow:yyyyMMdd_HHmmss}-{shortGuid}.json";

            var container = new BlobContainerClient(_config.StorageConnectionString, _config.ResultContainer);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlobClient(blobName);
            string json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blob.UploadAsync(stream, overwrite: true);

            // ── Table: summary row ────────────────────────────────────────────────────
            var table = new TableClient(_config.StorageConnectionString, _config.ResultTable);
            await table.CreateIfNotExistsAsync();

            string runId = Guid.NewGuid().ToString();
            var entity = new TableEntity(DateTime.UtcNow.ToString("yyyyMMdd"), runId)
            {
                { "ExperimentId", runId },
                { "Query", query },
                { "BestMatchingMovie", response.BestMatch },
                { "SimilarityScore", response.BestScore },
                { "ResultFileUrl", blob.Uri.ToString() },
                { "StartTimeUtc", startUtc },
                { "EndTimeUtc", DateTime.UtcNow },
                { "DurationSec", (long)(DateTime.UtcNow - startUtc).TotalSeconds }
            };
            await table.UpsertEntityAsync(entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Saved recommendations for \"{query}\" to Blob Storage and Table Storage.");
            sb.AppendLine($"Blob: {_config.ResultContainer}/{blobName}");
            sb.AppendLine($"Table row: {_config.ResultTable} (PartitionKey={entity.PartitionKey}, RowKey={runId})");
            sb.AppendLine();
            sb.AppendLine(response.FormattedText);

            return sb.ToString().TrimEnd();
        }
    }
}
