using AIRecommendation.Application.Interfaces;

namespace AIRecommendation.Infrastructure.Embeddings;

/// <summary>
/// A dummy implementation of <see cref="IEmbeddingService"/> that generates deterministic vectors for testing or demonstration.
/// </summary>
public class DummyEmbeddingService : IEmbeddingService
{
    /// <summary>
    /// Generates a deterministic numerical vector from the provided text using a custom hashing algorithm.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <returns>A float array representing the deterministic embedding vector.</returns>
    public float[] GenerateEmbedding(string text)
    {
        const int dim = 128;
        var v = new float[dim];

        unchecked
        {
            int hash = 17;
            foreach (var ch in text)
                hash = hash * 31 + ch;

            var rand = new Random(hash);
            for (int i = 0; i < dim; i++)
                v[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
        }

        return v;
    }
}