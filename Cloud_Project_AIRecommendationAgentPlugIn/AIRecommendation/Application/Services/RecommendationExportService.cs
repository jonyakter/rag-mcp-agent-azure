using AIRecommendation.Application.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AIRecommendation.Application.Services;

/// <summary>
/// A single ranked recommendation as written to the export JSON file.
/// </summary>
public class RecommendationResultExport
{
    /// <summary>Gets or sets the 1-based rank of this result within the returned list.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the display title of the recommended item.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the dataset category of the recommended item.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the source file name of the recommended item.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional URL link to the item.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets the display-scaled match accuracy, in the range 0.0-1.0 (see RecommendationSettings.ScoreDisplayCeiling).</summary>
    public double AccuracyScore { get; set; }

    /// <summary>Gets or sets the display-scaled match accuracy as a percentage, in the range 0-100.</summary>
    public double AccuracyPercent { get; set; }

    /// <summary>Gets or sets the unscaled cosine similarity score (0.0-1.0), kept for transparency.</summary>
    public double RawCosineScore { get; set; }
}

/// <summary>
/// The full set of recommendation results for a single query, as written to the export JSON file.
/// </summary>
public class RecommendationExport
{
    /// <summary>Gets or sets the original user query.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets the category detected from the query, if any.</summary>
    public string? DetectedCategory { get; set; }

    /// <summary>Gets or sets the requested maximum number of results.</summary>
    public int TopN { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the export was generated.</summary>
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Gets or sets the mean match accuracy (percentage) across all returned results.</summary>
    public double AverageAccuracyPercent { get; set; }

    /// <summary>Gets or sets the ranked recommendation results.</summary>
    public List<RecommendationResultExport> Results { get; set; } = new();
}

/// <summary>
/// Saves recommendation results to a JSON file so query outcomes and their match
/// accuracy (cosine similarity score) can be inspected or processed outside the console.
/// The output folder is config-driven via ExportSettings (appsettings.json) - not hardcoded.
/// </summary>
public class RecommendationExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _outputFolder;

    /// <summary>
    /// Initializes a new instance of <see cref="RecommendationExportService"/>.
    /// </summary>
    /// <param name="exportSettings">Export configuration from appsettings.json.</param>
    public RecommendationExportService(IOptions<ExportSettings> exportSettings)
    {
        var configuredPath = exportSettings.Value.OutputFolderPath;

        // Resolve relative paths against the solution root, matching the convention
        // used for Import:DataFolderPath so the folder works from Visual Studio or the CLI.
        _outputFolder = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", configuredPath));
    }

    /// <summary>
    /// Writes the given recommendation results to a timestamped JSON file.
    /// </summary>
    /// <param name="query">The original user query.</param>
    /// <param name="detectedCategory">The category detected from the query, if any.</param>
    /// <param name="topN">The requested maximum number of results.</param>
    /// <param name="results">The ranked recommendation results to export.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The full path of the JSON file that was written, and the average accuracy percentage.</returns>
    public async Task<(string FilePath, double AverageAccuracyPercent)> ExportAsync(
        string query,
        string? detectedCategory,
        int topN,
        IReadOnlyList<ScoredResult> results,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_outputFolder);

        double averageAccuracyPercent = results.Count == 0
            ? 0
            : Math.Round(results.Average(r => r.DisplayScore) * 100, 2);

        var export = new RecommendationExport
        {
            Query = query,
            DetectedCategory = string.IsNullOrWhiteSpace(detectedCategory) ? null : detectedCategory,
            TopN = topN,
            GeneratedAtUtc = DateTime.UtcNow,
            AverageAccuracyPercent = averageAccuracyPercent,
            Results = results.Select((r, i) => new RecommendationResultExport
            {
                Rank = i + 1,
                Title = string.IsNullOrWhiteSpace(r.Doc.Title) ? r.Doc.FileName : r.Doc.Title,
                Category = r.Doc.Category,
                FileName = r.Doc.FileName,
                Url = r.Doc.Url,
                AccuracyScore = Math.Round(r.DisplayScore, 4),
                AccuracyPercent = Math.Round(r.DisplayScore * 100, 2),
                RawCosineScore = Math.Round(r.Score, 4)
            }).ToList()
        };

        string fileName = $"recommendation_{export.GeneratedAtUtc:yyyyMMdd_HHmmssfff}.json";
        string fullPath = Path.Combine(_outputFolder, fileName);

        string json = JsonSerializer.Serialize(export, SerializerOptions);
        await File.WriteAllTextAsync(fullPath, json, ct);

        return (fullPath, averageAccuracyPercent);
    }
}
