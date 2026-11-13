using AIRecommendation.Infrastructure.Parsing;
using Xunit;

namespace UnitTests;

public class TxtDocumentParserTests
{
    [Theory]
    [InlineData(".txt")]
    [InlineData(".TXT")]
    [InlineData(".Txt")]
    public void CanParse_TxtExtensionCaseInsensitive_ReturnsTrue(string extension)
    {
        var parser = new TxtDocumentParser();

        Assert.True(parser.CanParse(extension));
    }

    [Theory]
    [InlineData(".csv")]
    [InlineData(".pdf")]
    [InlineData("")]
    public void CanParse_NonTxtExtension_ReturnsFalse(string extension)
    {
        var parser = new TxtDocumentParser();

        Assert.False(parser.CanParse(extension));
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFileContent()
    {
        var parser = new TxtDocumentParser();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "Wait Until Dark is a horror movie.");

            var text = await parser.ExtractTextAsync(path);

            Assert.Equal("Wait Until Dark is a horror movie.", text);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
