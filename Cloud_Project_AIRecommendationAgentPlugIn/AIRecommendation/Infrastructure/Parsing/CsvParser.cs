using AIRecommendation.Application.Interfaces;
using System.Text;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// A high-level CSV parsing service that combines full-text extraction
/// (<see cref="CsvDocumentParser"/>) with structured row-by-row reading
/// (<see cref="CsvRowsReader"/>).
/// <para>
/// Inject this class directly when you need both capabilities in one place,
/// e.g. <c>private readonly CsvParser _csvParser;</c>.
/// </para>
/// </summary>
public sealed class CsvParser : IDocumentParser
{
    // ── IDocumentParser support ──────────────────────────────────────────────

    private static readonly string[] SupportedExtensions = { ".csv" };

    /// <inheritdoc />
    public bool CanParse(string extension)
        => extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asynchronously reads the entire CSV file and returns its raw text content
    /// with normalised line endings.
    /// </summary>
    /// <param name="path">Full path to the CSV file.</param>
    /// <returns>Raw text content of the file.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public async Task<string> ExtractTextAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("CSV file not found.", path);

        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);

        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // Normalise line endings to the platform default
        return content
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", Environment.NewLine);
    }

    // ── Structured row reading ───────────────────────────────────────────────

    /// <summary>
    /// Reads all data rows from a CSV file and returns each row as a
    /// <c>Dictionary&lt;string, string&gt;</c> keyed by column header.
    /// </summary>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <param name="delimiter">
    /// Column delimiter character. Defaults to <c>','</c>.
    /// Use <c>';'</c> for European-style CSV files (e.g. restaurant datasets).
    /// </param>
    /// <returns>
    /// An enumerable of row dictionaries; the first CSV line is treated as the
    /// header row and is not included in the output.
    /// </returns>
    /// <example>
    /// <code>
    /// var rows = _csvParser.ReadRows("books.csv");
    /// foreach (var row in rows)
    ///     Console.WriteLine(row["Title"]);
    /// </code>
    /// </example>
    public IEnumerable<Dictionary<string, string>> ReadRows(string filePath, char delimiter = ',')
        => CsvRowsReader.ReadRows(filePath, delimiter);

    /// <summary>
    /// Auto-detects the delimiter from the file path / name and then reads all
    /// rows. Files whose path contains "restaurant" or "resturant" (case-insensitive)
    /// use <c>';'</c>; everything else uses <c>','</c>.
    /// </summary>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <returns>An enumerable of row dictionaries.</returns>
    public IEnumerable<Dictionary<string, string>> ReadRowsAutoDelimiter(string filePath)
    {
        bool isRestaurant = filePath.Contains("Restaurant", StringComparison.OrdinalIgnoreCase)
                         || filePath.Contains("Resturant",  StringComparison.OrdinalIgnoreCase);

        char delimiter = isRestaurant ? ';' : ',';
        return CsvRowsReader.ReadRows(filePath, delimiter);
    }

    // ── Metadata helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the number of data rows in a CSV file (excluding the header line).
    /// </summary>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <param name="delimiter">Column delimiter (defaults to <c>','</c>).</param>
    public int CountRows(string filePath, char delimiter = ',')
        => CsvRowsReader.ReadRows(filePath, delimiter).Count();

    /// <summary>
    /// Returns the column headers of a CSV file.
    /// </summary>
    /// <param name="filePath">Full path to the CSV file.</param>
    /// <param name="delimiter">Column delimiter (defaults to <c>','</c>).</param>
    /// <returns>An array of header strings, or an empty array if the file is empty.</returns>
    public string[] GetHeaders(string filePath, char delimiter = ',')
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0) return Array.Empty<string>();

        // Reuse the same parsing logic via a single-row read
        return CsvRowsReader.ReadRows(filePath, delimiter)
            .FirstOrDefault()
            ?.Keys
            .ToArray()
            ?? Array.Empty<string>();
    }
}
