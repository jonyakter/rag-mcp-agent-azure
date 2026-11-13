using System.Text.RegularExpressions;
using AIRecommendation.Application.Configuration;
using AIRecommendation.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// A text chunker that splits document content based on specific "ID:" markers.
/// The maximum chunk length is configurable via ImportSettings.MaxChunkLength.
/// </summary>
public sealed class IdBlockChunker : ITextChunker
{
    private readonly int _maxChunkLength;

    /// <summary>
    /// Initializes a new instance of <see cref="IdBlockChunker"/>.
    /// </summary>
    /// <param name="importSettings">
    /// Import configuration — provides MaxChunkLength.
    /// Defaults to 4000 characters if not configured.
    /// </param>
    public IdBlockChunker(IOptions<ImportSettings> importSettings)
    {
        _maxChunkLength = importSettings.Value.MaxChunkLength;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Chunk(string text, string? sourceName = null, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        // Split by: ID: 001 (start-of-line)
        var matches = Regex.Matches(text, @"(?im)^\s*ID\s*:\s*\d+");
        if (matches.Count == 0)
            return Array.Empty<string>();

        var chunks = new List<string>(matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;

            var chunk = text.Substring(start, end - start).Trim();
            if (string.IsNullOrWhiteSpace(chunk))
                continue;

            // Truncate to the configured max length (from ImportSettings.MaxChunkLength)
            if (chunk.Length > _maxChunkLength)
                chunk = chunk[.._maxChunkLength];

            chunks.Add(chunk);
        }

        return chunks;
    }
}