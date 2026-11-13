namespace AIRecommendation.Application.Interfaces;

/// <summary>
/// Defines a contract for splitting large text blocks into smaller, semantic chunks.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits the extracted text into smaller semantic chunks to store as separate document records.
    /// </summary>
    /// <param name="text">The full text to be chunked.</param>
    /// <param name="sourceName">Optional name of the source document.</param>
    /// <param name="category">Optional category of the document.</param>
    /// <returns>A list of text chunks. Returns an empty list if no chunking is performed (caller should fallback to full text).</returns>
    IReadOnlyList<string> Chunk(string text, string? sourceName = null, string? category = null);
}