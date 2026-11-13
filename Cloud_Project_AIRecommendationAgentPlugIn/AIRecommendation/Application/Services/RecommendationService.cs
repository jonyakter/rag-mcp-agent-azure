using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Interfaces;
using AIRecommendation.Domain.Entities;
using AIRecommendation.Infrastructure.Similarity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AIRecommendation.Application.Services;

/// <summary>
/// Represents a document record paired with its semantic similarity score for a given query.
/// </summary>
public class ScoredResult
{
    /// <summary>Gets or sets the document record.</summary>
    public DocumentRecord Doc { get; set; } = null!;

    /// <summary>Gets or sets the raw cosine similarity score (0-1). Used for ranking and MinScoreThreshold filtering.</summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets the display-scaled match score (0-1): Score divided by
    /// RecommendationSettings.ScoreDisplayCeiling and clamped to 1.0. This is what should be
    /// shown to users/exported, since raw cosine similarity rarely approaches 1.0 even for a
    /// genuinely strong match.
    /// </summary>
    public double DisplayScore { get; set; }
}

/// <summary>
/// Performs semantic search against the document repository using vector embeddings.
/// Registered in the DI container - no hard-coded values.
/// Thresholds and result counts are read from RecommendationSettings (appsettings.json).
/// </summary>
public class RecommendationService
{
    private readonly IDocumentRepository _repository;
    private readonly IEmbeddingService _embeddings;
    private readonly double _minScoreThreshold;
    private readonly double _scoreDisplayCeiling;
    private readonly ILogger<RecommendationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RecommendationService"/>.
    /// All dependencies are injected by the DI container.
    /// </summary>
    /// <param name="repository">The document repository to search.</param>
    /// <param name="embeddings">The embedding service to vectorise queries.</param>
    /// <param name="recommendationSettings">
    /// Recommendation engine settings from appsettings.json.
    /// Provides MinScoreThreshold — replaces the old hardcoded 0.30 constant.
    /// </param>
    public RecommendationService(
        IDocumentRepository repository,
        IEmbeddingService embeddings,
        IOptions<RecommendationSettings> recommendationSettings,
        ILogger<RecommendationService> logger)
    {
        _repository = repository;
        _embeddings = embeddings;
        _minScoreThreshold = recommendationSettings.Value.MinScoreThreshold;
        _scoreDisplayCeiling = recommendationSettings.Value.ScoreDisplayCeiling;
        _logger = logger;
    }

    /// <summary>
    /// Searches for documents most semantically similar to the user query.
    /// </summary>
    /// <param name="query">The user's natural-language query.</param>
    /// <param name="category">Optional category filter (e.g. "books", "restaurant").</param>
    /// <param name="topN">Maximum number of results to return.</param>
    /// <returns>A ranked list of <see cref="ScoredResult"/> objects.</returns>
    public async Task<List<ScoredResult>> SearchAsync(string query, string? category, int topN)
    {
        // Generate embedding for the query using the configured service (OpenAI or Dummy)
        var queryVec = _embeddings.GenerateEmbedding(query);

        // Load all candidates from the repository (filtered by category when provided)
        var docs = await _repository.GetAllAsync(category);

        // Diagnostic: warn if vector dimensions are inconsistent
        if (docs.Count > 0)
        {
            var firstVec = JsonSerializer.Deserialize<float[]>(docs[0].EmbeddingJson) ?? Array.Empty<float>();
            if (queryVec.Length != firstVec.Length)
            {
                _logger.LogWarning("[Diagnostic] Vector length mismatch: Query={QueryLength}, DB={DbLength}. Re-run ImporterApp to regenerate embeddings with the current service.", queryVec.Length, firstVec.Length);
            }
        }

        // Rank by cosine similarity and return the top-N above the configured threshold.
        // Ranking/filtering use the raw score; DisplayScore is only for presentation.
        return docs
            .Select(d =>
            {
                double raw = Cosine.Similarity(
                    queryVec,
                    JsonSerializer.Deserialize<float[]>(d.EmbeddingJson) ?? Array.Empty<float>());

                double display = _scoreDisplayCeiling > 0
                    ? Math.Clamp(raw / _scoreDisplayCeiling, 0.0, 1.0)
                    : raw;

                return new ScoredResult { Doc = d, Score = raw, DisplayScore = display };
            })
            .Where(r => r.Score >= _minScoreThreshold)
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();
    }
}