// The MCP server calls OpenAI itself to embed queries, matching the OpenAI-generated
// embeddings already stored in the database (see AIRecommendation.Infrastructure.Embeddings).

using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Interfaces;
using AIRecommendation.Infrastructure.Embeddings;
using AIRecommendation.Infrastructure.Persistence;
using AIRecommendation.Infrastructure.Similarity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyCloudProject.Common;
using System.Text.Json;

namespace MyExperiment.Mcp
{
    /// <summary>
    /// Shared service injected into MCP tool classes.
    /// Holds the document repository (Azure SQL) and the OpenAI embedding service.
    /// Registered as Singleton in Program.cs — created once per server lifetime.
    ///
    /// Mirrors: MonkeyService in professor's SampleMcphttp exercise.
    /// </summary>
    public class RecommendationService
    {
        private readonly McpConfig _config;

        /// <summary>
        /// OpenAI embedding service. Must match the model used to build the stored
        /// document embeddings (text-embedding-3-small, 1536 dimensions) — cosine
        /// similarity returns 0 for any length mismatch (see Cosine.Similarity).
        /// </summary>
        public IEmbeddingService EmbeddingService { get; }

        /// <summary>
        /// Document repository reading from Azure SQL Database.
        /// Used by CosineSimilarityTool to load stored embeddings.
        /// </summary>
        public IDocumentRepository Repository { get; }

        public RecommendationService(
            IOptions<McpConfig> config,
            IOptions<OpenAiSettings> openAiConfig,
            IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;

            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("https://api.openai.com/");
            EmbeddingService = new OpenAiEmbeddingService(httpClient, openAiConfig);

            // Document repository against Azure SQL
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_config.SqlConnectionString)
                .Options;

            Repository = new EfDocumentRepository(new AppDbContext(dbOptions));
        }

        /// <summary>
        /// Embeds the query, ranks stored documents by cosine similarity, and builds the
        /// formatted response. Shared by RecommendationTools.Recommend and
        /// SaveRecommendationTools.SaveRecommendation so both always agree on results.
        /// </summary>
        public async Task<McpRecommendResponse> BuildRecommendationAsync(string query, string? category)
        {
            float[] queryVector = EmbeddingService.GenerateEmbedding(query);

            string? cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            var docs = await Repository.GetAllAsync(cat);

            var ranked = docs
                .Select(doc =>
                {
                    float[] docVec = JsonSerializer.Deserialize<float[]>(doc.EmbeddingJson)
                                     ?? Array.Empty<float>();
                    double raw = Cosine.Similarity(queryVector, docVec);
                    double display = _config.ScoreDisplayCeiling > 0
                        ? Math.Clamp(raw / _config.ScoreDisplayCeiling, 0.0, 1.0)
                        : raw;
                    return (doc, raw, display);
                })
                .Where(r => r.raw >= _config.MinScoreThreshold)
                .OrderByDescending(r => r.raw)
                .Take(_config.DefaultTopN)
                .ToList();

            var sb = new System.Text.StringBuilder();
            if (ranked.Count == 0)
            {
                sb.AppendLine("No relevant matches found.");
            }
            else
            {
                sb.AppendLine($"Recommendations for: \"{query}\"");
                sb.AppendLine();
                for (int i = 0; i < ranked.Count; i++)
                {
                    var (doc, raw, display) = ranked[i];
                    string title = string.IsNullOrWhiteSpace(doc.Title) ? doc.FileName : doc.Title;
                    sb.AppendLine($"{i + 1}. {title}  (match: {display:P0})");
                }
            }

            var items = ranked.Select((r, i) => new McpRecommendationItem
            {
                Rank = i + 1,
                Title = string.IsNullOrWhiteSpace(r.doc.Title) ? r.doc.FileName : r.doc.Title,
                FileName = r.doc.FileName,
                Category = r.doc.Category,
                Score = r.raw
            }).ToList();

            string bestTitle = items.FirstOrDefault()?.Title ?? string.Empty;
            double bestScore = ranked.FirstOrDefault().raw;

            return new McpRecommendResponse
            {
                QueryText = query,
                BestMatch = bestTitle,
                BestScore = bestScore,
                Items = items,
                FormattedText = sb.ToString().TrimEnd()
            };
        }
    }
}