using AIRecommendation.Infrastructure.Similarity;
using Xunit;

namespace UnitTests;

public class CosineTests
{
    [Fact]
    public void Similarity_IdenticalVectors_ReturnsApproximatelyOne()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 1f, 2f, 3f };

        var result = Cosine.Similarity(a, b);

        Assert.True(result > 0.999);
    }

    [Fact]
    public void Similarity_OrthogonalVectors_ReturnsApproximatelyZero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };

        var result = Cosine.Similarity(a, b);

        Assert.True(Math.Abs(result) < 1e-6);
    }

    [Fact]
    public void Similarity_OppositeVectors_ReturnsApproximatelyMinusOne()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { -1f, 0f };

        var result = Cosine.Similarity(a, b);

        Assert.True(result < -0.999);
    }

    [Fact]
    public void Similarity_MismatchedLengths_ReturnsZero()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 1f, 2f };

        var result = Cosine.Similarity(a, b);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Similarity_EmptyVectors_ReturnsZero()
    {
        var result = Cosine.Similarity(Array.Empty<float>(), Array.Empty<float>());

        Assert.Equal(0, result);
    }
}
