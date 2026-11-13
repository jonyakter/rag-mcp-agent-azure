namespace AIRecommendation.Application.Interfaces;

/// <summary>
/// Defines a contract for generating vector embeddings from text strings.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a numerical vector (embedding) representing the semantic meaning of the provided text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <returns>An array of floats representing the embedding vector.</returns>
    float[] GenerateEmbedding(string text);
}