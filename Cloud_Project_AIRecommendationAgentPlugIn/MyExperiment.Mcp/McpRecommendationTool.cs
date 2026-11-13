
// This is the FULL recommendation pipeline tool.
// It returns formatted readable text + BestMatch + BestScore + Items list
// serialised as McpRecommendResponse JSON — exactly what McpClient.cs expects.
//
// CosineSimilarityTool.cs returns raw debug JSON (scores only).
// THIS tool returns the complete McpRecommendResponse that Experiment.cs reads.
//
// The agent calls this as "Recommend" in McpClient.cs:
//   client.CallToolAsync("Recommend", new { query = queryText })

using ModelContextProtocol.Server;
using MyCloudProject.Common;
using System.ComponentModel;
using System.Text.Json;

namespace MyExperiment.Mcp
{
    [McpServerToolType]
    public sealed class RecommendationTools
    {
        private readonly RecommendationService _service;

        public RecommendationTools(RecommendationService service)
        {
            _service = service;
        }

        [McpServerTool, Description(
            "Finds the most relevant recommendations for a natural-language query. " +
            "Returns a formatted recommendation list with titles and similarity scores. " +
            "Use this for movie, book, or restaurant recommendations.")]
        public async Task<string> Recommend(
            [Description("Natural-language query, e.g. 'feel-good sci-fi from the 1980s'")]
            string query,
            [Description("Optional category: 'movies', 'books', 'restaurant'. Empty = all.")]
            string category = "")
        {
            if (string.IsNullOrWhiteSpace(query))
                return JsonSerializer.Serialize(new McpRecommendResponse
                {
                    QueryText = query,
                    FormattedText = "Please provide a query.",
                    BestMatch = "",
                    BestScore = 0
                });

            var response = await _service.BuildRecommendationAsync(query, category);

            // Return as JSON — McpClient.cs deserialises this into McpRecommendResponse
            return JsonSerializer.Serialize(response);
        }
    }
}