using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Interfaces;
using AIRecommendation.Infrastructure.Similarity;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AIRecommendation.Application.Tools;

/// <summary>
/// Represents a single entry in a chat conversation.
/// </summary>
/// <param name="Role">The role of the speaker (e.g., "User", "Assistant").</param>
/// <param name="Content">The text content of the message.</param>
/// <param name="TimestampUtc">The UTC timestamp when the message was created.</param>
public sealed record ChatEntry(string Role, string Content, DateTime TimestampUtc);

/// <summary>
/// Defines a contract for a tool that can be executed by the recommendation agent.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the tool logic based on the user intent and conversation history.
    /// </summary>
    /// <param name="intentText">The processed intent or user input.</param>
    /// <param name="history">The previous chat entries for context.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the tool response string.</returns>
    Task<string> ExecuteAsync(string intentText, IEnumerable<ChatEntry>? history, CancellationToken ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// TOOL 3 — EmbeddingTool (NEW)
// File: AIRecommendation/Application/Tools/RecommendationTools.cs
// This tool wraps IEmbeddingService so the embedding step is independently
// callable as a plugin. It takes a text string and returns the float vector
// as a JSON string. Later this becomes an MCP tool for the cloud project.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A tool that generates a vector embedding for a given text input.
/// Wraps IEmbeddingService so the embedding step is independently callable as a plugin.
/// Input:  any text string (e.g. "find me a scary book")
/// Output: JSON array of floats e.g. "[0.123, -0.456, 0.789, ...]"
/// Usage in console: prefix your query with "embed:" e.g. "embed: Italian restaurant"
/// </summary>
public sealed class EmbeddingTool : ITool
{
    private readonly IEmbeddingService _embeddingService;

    public EmbeddingTool(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public string Name => "GenerateEmbedding";

    public Task<string> ExecuteAsync(string intentText, IEnumerable<ChatEntry>? history, CancellationToken ct)
    {
        // Strip the "embed:" prefix if the user typed it directly in the console
        var text = intentText.StartsWith("embed:", StringComparison.OrdinalIgnoreCase)
            ? intentText["embed:".Length..].Trim()
            : intentText.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult("Please provide text to embed.");

        float[] vector = _embeddingService.GenerateEmbedding(text);

        // Return vector dimensions and first few values as a summary,
        // plus the full JSON so it can be consumed by other tools or tests.
        var preview = vector.Take(5).Select(v => Math.Round(v, 4));
        string fullJson = JsonSerializer.Serialize(vector);

        var result = new StringBuilder();
        result.AppendLine($"Embedding generated successfully.");
        result.AppendLine($"Dimensions : {vector.Length}");
        result.AppendLine($"Preview    : [{string.Join(", ", preview)}, ...]");
        result.AppendLine($"Full vector: {fullJson}");

        return Task.FromResult(result.ToString().TrimEnd());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TOOL 4 — CosineSimilarityTool (NEW)
// File: AIRecommendation/Application/Tools/RecommendationTools.cs
// This tool exposes the embedding + cosine similarity computation as a
// standalone callable plugin, separate from formatting and export logic.
// Later this becomes an MCP tool for the cloud project.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A tool that computes cosine similarity between a query and all stored documents.
/// It embeds the query, loads all documents from the DB, scores each one, and
/// returns the top-N results as a JSON string.
/// Input:  any text query (e.g. "cosine: find me a thriller book")
/// Output: JSON array of ranked results with Title, Category, RawScore, DisplayScore
/// Usage in console: prefix your query with "cosine:" e.g. "cosine: Italian restaurant"
/// </summary>
public sealed class CosineSimilarityTool : ITool
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _repository;
    private readonly double _minScoreThreshold;
    private readonly double _scoreDisplayCeiling;
    private readonly int _defaultTopN;

    public CosineSimilarityTool(
        IEmbeddingService embeddingService,
        IDocumentRepository repository,
        IOptions<RecommendationSettings> settings)
    {
        _embeddingService = embeddingService;
        _repository = repository;
        _minScoreThreshold = settings.Value.MinScoreThreshold;
        _scoreDisplayCeiling = settings.Value.ScoreDisplayCeiling;
        _defaultTopN = settings.Value.DefaultTopN;
    }

    public string Name => "ComputeCosineSimilarity";

    public async Task<string> ExecuteAsync(string intentText, IEnumerable<ChatEntry>? history, CancellationToken ct)
    {
        // Strip the "cosine:" prefix if the user typed it directly in the console
        var query = intentText.StartsWith("cosine:", StringComparison.OrdinalIgnoreCase)
            ? intentText["cosine:".Length..].Trim()
            : intentText.Trim();

        if (string.IsNullOrWhiteSpace(query))
            return "Please provide a query to compute similarity for.";

        // Step 1: Generate embedding for the query
        float[] queryVector = _embeddingService.GenerateEmbedding(query);

        // Step 2: Load all documents from the database (no category filter — tool is general)
        var docs = await _repository.GetAllAsync(null);

        if (docs.Count == 0)
            return "No documents found in the database. Please run ImporterApp first.";

        // Step 3: Compute cosine similarity for each document and rank results
        var results = docs
            .Select(doc =>
            {
                float[] docVector = JsonSerializer.Deserialize<float[]>(doc.EmbeddingJson)
                    ?? Array.Empty<float>();

                double raw = Cosine.Similarity(queryVector, docVector);

                double display = _scoreDisplayCeiling > 0
                    ? Math.Clamp(raw / _scoreDisplayCeiling, 0.0, 1.0)
                    : raw;

                return new
                {
                    Title = string.IsNullOrWhiteSpace(doc.Title) ? doc.FileName : doc.Title,
                    doc.Category,
                    RawScore = Math.Round(raw, 4),
                    DisplayScore = Math.Round(display, 4),
                    AccuracyPct = Math.Round(display * 100, 2)
                };
            })
            .Where(r => r.RawScore >= _minScoreThreshold)
            .OrderByDescending(r => r.RawScore)
            .Take(_defaultTopN)
            .ToList();

        if (results.Count == 0)
            return $"No results met the minimum score threshold ({_minScoreThreshold}).";

        // Return as JSON so the output can be consumed by tests or the cloud project
        var output = new StringBuilder();
        output.AppendLine($"Cosine similarity results for: \"{query}\"");
        output.AppendLine($"Returned {results.Count} result(s) above threshold {_minScoreThreshold}:");
        output.AppendLine();
        output.AppendLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

        return output.ToString().TrimEnd();
    }
}

