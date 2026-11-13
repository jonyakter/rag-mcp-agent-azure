using AIRecommendation.Application.Services;
using AIRecommendation.Domain.Entities;
using Xunit;

namespace UnitTests;

public class RecommendationFormatterTests
{
    [Fact]
    public void FormatResults_EmptyList_ReturnsNoMatchesMessage()
    {
        var result = RecommendationFormatter.FormatResults(
            Enumerable.Empty<ScoredResult>(), detectedCategory: "movie", topN: 5);

        Assert.Equal("No relevant matches found.", result);
    }

    [Fact]
    public void FormatResults_IncludesTitleCategoryAndScore()
    {
        var scored = new ScoredResult
        {
            Doc = new DocumentRecord
            {
                Title = "Wait Until Dark",
                FileName = "movies.csv",
                Category = "movie",
                Content = "Horror|1967|Terence Young",
            },
            Score = 0.78,
            DisplayScore = 0.78,
        };

        var result = RecommendationFormatter.FormatResults(
            new[] { scored }, detectedCategory: "movie", topN: 5);

        Assert.Contains("Wait Until Dark", result);
        Assert.Contains("[MOVIE] RECOMMENDATION", result);
        Assert.Contains("78.00", result);
    }

    [Fact]
    public void FormatResults_NoDisplayLabelProvided_DerivesLabelFromCategory()
    {
        var scored = new ScoredResult
        {
            Doc = new DocumentRecord { Title = "Frankfurter Wirtshaus", Category = "restaurant" },
            Score = 0.9,
            DisplayScore = 0.9,
        };

        var result = RecommendationFormatter.FormatResults(
            new[] { scored }, detectedCategory: "restaurant", topN: 5);

        Assert.Contains("Restaurant Recommendation", result);
    }

    [Fact]
    public void FormatResults_MissingTitle_FallsBackToFileName()
    {
        var scored = new ScoredResult
        {
            Doc = new DocumentRecord { Title = "", FileName = "dataset-row-12.csv", Category = "movie" },
            Score = 0.5,
            DisplayScore = 0.5,
        };

        var result = RecommendationFormatter.FormatResults(
            new[] { scored }, detectedCategory: "movie", topN: 5);

        Assert.Contains("dataset-row-12.csv", result);
    }
}
