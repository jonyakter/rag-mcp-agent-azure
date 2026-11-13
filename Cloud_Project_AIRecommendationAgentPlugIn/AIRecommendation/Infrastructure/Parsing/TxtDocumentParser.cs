using AIRecommendation.Application.Interfaces;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// A parser that extracts text content from plain text (.txt) files.
/// </summary>
public class TxtDocumentParser : IDocumentParser
{
    /// <inheritdoc />
    public bool CanParse(string extension)
        => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asynchronously extracts all text from a plain text file.
    /// </summary>
    /// <param name="path">The full path to the text file.</param>
    /// <returns>A task representing the asynchronous operation, containing the extracted text.</returns>
    public Task<string> ExtractTextAsync(string path)
        => File.ReadAllTextAsync(path);
}