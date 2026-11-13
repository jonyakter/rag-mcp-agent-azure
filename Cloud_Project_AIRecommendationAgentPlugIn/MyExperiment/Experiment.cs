// Architecture (correct flow):
//   1. Agent downloads query.txt from Azure Blob Storage
//   2. Agent calls MCP "Recommend" tool with query text
//   3. MCP server handles embedding (OpenAI) + cosine similarity search internally
//   4. Agent writes result file locally, uploads to Blob result container
//   5. Agent writes ExperimentResult row to Azure Table Storage

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyCloudProject.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyExperiment
{
    /// <summary>
    /// Runs the AI recommendation experiment in the cloud.
    ///
    /// Flow:
    ///   1. Download the query input file from Azure Blob Storage.
    ///   2. Call the MCP server "Recommend" tool with the query text.
    ///      The MCP server handles OpenAI embedding + cosine similarity internally.
    ///   3. Write the result text file and upload it to the result Blob container.
    ///   4. Write the experiment result row to Azure Table Storage.
    /// </summary>
    public class Experiment : IExperiment
    {
        private readonly IStorageProvider _storageProvider;
        private readonly ILogger _logger;
        private readonly MyConfig _config;
        private readonly McpClient _mcpClient;

        public Experiment(IConfigurationSection configSection, IStorageProvider storageProvider, ILogger log)
        {
            _storageProvider = storageProvider;
            _logger = log;

            _config = new MyConfig();
            configSection.Bind(_config);

            // MCP client — connects to the MCP server deployed on Azure Container Apps.
            // The MCP server handles OpenAI embedding internally — Agent does NOT call OpenAI.
            _mcpClient = new McpClient(_config.McpServerUrl);
        }

        /// <inheritdoc/>
        public async Task<IExperimentResult> RunAsync(string inputData)
        {
            var startUtc = DateTime.UtcNow;

            // ── Step 1: Read query text from downloaded file ──────────────────────────
            string query = ResolveQuery(inputData);
            _logger.LogInformation("Recommendation query: {Query}", query);

            // ── Step 2: Call MCP server ───────────────────────────────────────────────
            // MCP server does the OpenAI embedding + cosine similarity search.
            // Agent only passes the raw query text.
            _logger.LogInformation("Calling MCP server at {Url}...", _config.McpServerUrl);
            McpRecommendResponse mcp = await _mcpClient.FindSimilarAsync(
                queryVector: Array.Empty<float>(),
                queryText: query,
                category: null,
                topN: _config.TopN);

            string bestTitle = string.IsNullOrWhiteSpace(mcp.BestMatch) ? "none" : mcp.BestMatch;
            double topScore = mcp.BestScore;
            _logger.LogInformation("MCP response received. Best match: {Best}", bestTitle);

            Console.WriteLine();
            Console.WriteLine("===== Recommendation Result =====");
            Console.WriteLine($"Query: {query}");
            Console.WriteLine(mcp.FormattedText);
            Console.WriteLine("==================================");
            Console.WriteLine();

            // ── Step 3: Write result file locally ─────────────────────────────────────
            string resultPath = Path.Combine(
                AppContext.BaseDirectory,
                $"result_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(resultPath, mcp.FormattedText ?? string.Empty);
            _logger.LogInformation("Result file written to: {Path}", resultPath);

            var endUtc = DateTime.UtcNow;

            // ── Step 4: Build ExperimentResult ────────────────────────────────────────
            var result = new ExperimentResult(_config.GroupId, Guid.NewGuid().ToString())
            {
                ExperimentId = Guid.NewGuid().ToString(),
                Name = "AI Movie Recommendation",
                Description = $"Query: {query} | Best match: {bestTitle}",
                StartTimeUtc = startUtc,
                EndTimeUtc = endUtc,
                DurationSec = (long)(endUtc - startUtc).TotalSeconds,
                Duration = endUtc - startUtc,
                InputFileUrl = inputData,
                OutputFiles = new[] { resultPath },
                Accuracy = (float)topScore
            };

            _logger.LogInformation(
                "Experiment done. Best match: {Best} (score {Score:P2}). Result file: {Path}",
                bestTitle, topScore, resultPath);

            return result;
        }

        /// <summary>
        /// Reads the query text from the downloaded local file.
        /// Falls back to using inputData itself as the query if the file cannot be read.
        /// </summary>
        private static string ResolveQuery(string inputData)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(inputData) && File.Exists(inputData))
                {
                    var text = File.ReadAllText(inputData).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch { /* fall through */ }

            return string.IsNullOrWhiteSpace(inputData) ? "recommend something" : inputData.Trim();
        }
    }
}