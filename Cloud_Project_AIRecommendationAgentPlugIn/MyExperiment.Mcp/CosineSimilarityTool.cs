// Uses RecommendationService (injected) for embedding service + SQL repository.

using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Services;
using AIRecommendation.Application.Tools;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MyExperiment.Mcp
{
    /// <summary>
    /// MCP tool that wraps CosineSimilarityTool from the AIRecommendation SE library.
    /// Embeds the query internally and finds the most similar documents from Azure SQL.
    /// Uses RecommendationService (Singleton) for the embedding service and repository.
    /// </summary>
    [McpServerToolType]
    public sealed class CosineSimilarityTools
    {
        private readonly RecommendationService _service;
        private readonly McpConfig _config;

        // RecommendationService is injected — mirrors MonkeyTools(MonkeyService)
        public CosineSimilarityTools(RecommendationService service, IOptions<McpConfig> config)
        {
            _service = service;
            _config = config.Value;
        }

        [McpServerTool, Description(
            "Finds the most similar documents to a natural-language query using cosine similarity " +
            "against the Azure SQL embedding store. Embeds the query internally — the caller does " +
            "not need to provide a vector. Returns ranked results with Title, Category, RawScore, " +
            "DisplayScore. Results below MinScoreThreshold (0.30) are excluded.")]
        public async Task<string> FindSimilarDocuments(
            [Description("The natural-language query, e.g. 'feel-good sci-fi from the 1980s'")]
            string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Please provide a query to search for.";

            // Delegate to CosineSimilarityTool from the AIRecommendation SE library
            var tool = new AIRecommendation.Application.Tools.CosineSimilarityTool(
                _service.EmbeddingService,
                _service.Repository,
                Options.Create(new RecommendationSettings
                {
                    MinScoreThreshold = _config.MinScoreThreshold,
                    ScoreDisplayCeiling = _config.ScoreDisplayCeiling,
                    DefaultTopN = _config.DefaultTopN
                }));

            // Prefix "cosine:" — CosineSimilarityTool strips this prefix internally
            return await tool.ExecuteAsync("cosine: " + query, null, CancellationToken.None);
        }
    }
}