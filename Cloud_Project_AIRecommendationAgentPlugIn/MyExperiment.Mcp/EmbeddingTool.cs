// Uses RecommendationService (injected) to get the embedding service.

using AIRecommendation.Application.Services;
using AIRecommendation.Application.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MyExperiment.Mcp
{
    /// <summary>
    /// MCP tool that wraps EmbeddingTool from the AIRecommendation SE library.
    /// Takes a natural-language text and returns its float[] embedding vector.
    /// Uses RecommendationService (Singleton) for the embedding service.
    /// </summary>
    [McpServerToolType]
    public sealed class EmbeddingTools
    {
        private readonly RecommendationService _service;

        // RecommendationService is injected — mirrors MonkeyTools(MonkeyService)
        public EmbeddingTools(RecommendationService service)
        {
            _service = service;
        }

        [McpServerTool, Description(
            "Generates a float[] embedding vector for a natural-language text query " +
            "using the configured embedding service (OpenAI text-embedding-3-small or offline model). " +
            "Returns dimension count, a 5-value preview, and the full JSON float array.")]
        public async Task<string> GenerateEmbedding(
            [Description("The natural-language text to embed, e.g. 'sci-fi movie from the 1980s'")]
            string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Please provide a text query to embed.";

            // Delegate to EmbeddingTool from the AIRecommendation SE library
            var tool = new AIRecommendation.Application.Tools.EmbeddingTool(
                _service.EmbeddingService);

            return await tool.ExecuteAsync(query, null, CancellationToken.None);
        }
    }
}