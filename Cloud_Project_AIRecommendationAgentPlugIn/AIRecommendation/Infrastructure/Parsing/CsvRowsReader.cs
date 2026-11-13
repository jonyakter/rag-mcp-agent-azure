using System.Text.RegularExpressions;

namespace AIRecommendation.Infrastructure.Parsing;

/// <summary>
/// Provides utility methods for reading CSV files and returning them as a collection of dictionaries representing rows.
/// </summary>
public static class CsvRowsReader
{
    /// <summary>
    /// Reads rows from a CSV file and converts each row into a dictionary where keys are column headers.
    /// </summary>
    /// <param name="filePath">The full path to the CSV file.</param>
    /// <param name="delimiter">The delimiter character (defaults to comma).</param>
    /// <returns>An enumerable of dictionaries, one for each row.</returns>
    public static IEnumerable<Dictionary<string, string>> ReadRows(string filePath, char delimiter = ',')
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) yield break;

        var headers = ParseLine(lines[0], delimiter);

        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseLine(lines[i], delimiter);
            var row = new Dictionary<string, string>();

            for (int j = 0; j < headers.Length; j++)
            {
                var key = headers[j].Trim();
                var value = j < values.Length ? values[j].Trim() : "";
                row[key] = value;
            }
            yield return row;
        }
    }

    /// <summary>
    /// Parses a single CSV line into an array of strings, handling quoted values.
    /// </summary>
    /// <param name="line">The raw CSV line.</param>
    /// <param name="delimiter">The delimiter character.</param>
    /// <returns>An array of strings containing the parsed values.</returns>
    private static string[] ParseLine(string line, char delimiter)
    {
        // Simple CSV parser that handles quotes
        var pattern = $"(?<=^|{delimiter})(\"(?:[^\"]|\"\")*\"|[^{delimiter}]*)";
        var matches = Regex.Matches(line, pattern);
        var result = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            var val = matches[i].Value;
            if (val.StartsWith("\"") && val.EndsWith("\""))
            {
                val = val.Substring(1, val.Length - 2).Replace("\"\"", "\"");
            }
            result[i] = val;
        }
        return result;
    }
}