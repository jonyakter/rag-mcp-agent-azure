namespace AIRecommendation.Infrastructure.Similarity;

/// <summary>
/// Provides utility methods for calculating vector similarity using the cosine similarity metric.
/// </summary>
public static class Cosine
{
    /// <summary>
    /// Calculates the cosine similarity score between two float arrays.
    /// </summary>
    /// <param name="a">The first vector (e.g., query embedding).</param>
    /// <param name="b">The second vector (e.g., document embedding).</param>
    /// <returns>A double value between 0 and 1 representing the semantic similarity.</returns>
    /// <remarks>
    /// VITAL: If lengths don't match, the dot product is invalid.
    /// This often happens if data was imported with Dummy service (128 dimensions) 
    /// but searched with OpenAI service (1536 dimensions).
    /// </remarks>
    public static double Similarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        
        // Add a small epsilon to avoid division by zero
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-10);
    }
}