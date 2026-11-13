using AIRecommendation.Application.Interfaces;
using System.Text;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// A parser that extracts text content from CSV files.
/// </summary>
public class CsvDocumentParser : IDocumentParser
{
    /// <inheritdoc />
    public bool CanParse(string extension)
        => extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asynchronously extracts all text from a CSV file.
    /// </summary>
    /// <param name="path">The full path to the CSV file.</param>
    /// <returns>A task representing the asynchronous operation, containing the extracted text.</returns>
    /// <exception cref="ArgumentException">Thrown if the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public async Task<string> ExtractTextAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("CSV file not found.", path);

        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);

        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // Normalize line endings
        return content
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", Environment.NewLine);
    }
}