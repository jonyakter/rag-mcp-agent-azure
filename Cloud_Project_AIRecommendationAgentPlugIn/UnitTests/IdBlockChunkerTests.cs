using AIRecommendation.Application.Configuration;
using AIRecommendation.Infrastructure.Parsing;
using Microsoft.Extensions.Options;
using Xunit;

namespace UnitTests;

public class IdBlockChunkerTests
{
    private static IdBlockChunker CreateChunker(int maxChunkLength = 4000)
        => new(Options.Create(new ImportSettings { MaxChunkLength = maxChunkLength }));

    [Fact]
    public void Chunk_SplitsTextOnIdMarkers()
    {
        var chunker = CreateChunker();
        var text = "ID: 001\nFirst movie details\nID: 002\nSecond movie details";

        var chunks = chunker.Chunk(text);

        Assert.Equal(2, chunks.Count);
        Assert.StartsWith("ID: 001", chunks[0]);
        Assert.StartsWith("ID: 002", chunks[1]);
        Assert.DoesNotContain("ID: 002", chunks[0]);
    }

    [Fact]
    public void Chunk_NoIdMarkers_ReturnsEmpty()
    {
        var chunker = CreateChunker();

        var chunks = chunker.Chunk("Just plain text with no markers.");

        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_EmptyOrWhitespaceText_ReturnsEmpty()
    {
        var chunker = CreateChunker();

        Assert.Empty(chunker.Chunk(""));
        Assert.Empty(chunker.Chunk("   "));
    }

    [Fact]
    public void Chunk_TruncatesBlockLongerThanMaxChunkLength()
    {
        var chunker = CreateChunker(maxChunkLength: 20);
        var text = "ID: 001\n" + new string('x', 100);

        var chunks = chunker.Chunk(text);

        Assert.Single(chunks);
        Assert.Equal(20, chunks[0].Length);
    }
}
