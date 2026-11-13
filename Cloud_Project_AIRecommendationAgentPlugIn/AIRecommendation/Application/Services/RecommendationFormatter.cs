using AIRecommendation.Application.Services;

namespace AIRecommendation.Application.Services;

/// <summary>
/// Formats a list of <see cref="ScoredResult"/> objects into a human-readable recommendation string.
/// Category display labels are now derived dynamically from the category name, so adding
/// new categories to DatasetProfiles in appsettings.json requires no code change here.
/// </summary>
public static class RecommendationFormatter
{
    /// <summary>
    /// Formats a collection of scored results into a structured display string.
    /// </summary>
    /// <param name="results">The ranked recommendation results.</param>
    /// <param name="detectedCategory">The category detected from the user query.</param>
    /// <param name="topN">The maximum number of results that were requested.</param>
    /// <param name="displayLabel">
    /// Optional human-readable label for the detected intent (e.g. "Book Recommendation").
    /// When null or empty, a label is derived from the category name automatically.
    /// </param>
    /// <returns>A formatted string ready for console output.</returns>
    public static string FormatResults(
        IEnumerable<ScoredResult> results,
        string detectedCategory,
        int topN,
        string? displayLabel = null)
    {
        var list = results.ToList();
        if (list.Count == 0)
            return "No relevant matches found.";

        var sb = new System.Text.StringBuilder();

        // Show intent label. Use the provided label if available, otherwise derive from category.
        if (!string.IsNullOrWhiteSpace(detectedCategory))
        {
            var label = !string.IsNullOrWhiteSpace(displayLabel)
                ? displayLabel
                : DeriveLabel(detectedCategory);

            sb.AppendLine($"[Agent] Detected Intent: {label}");
        }

        sb.AppendLine();
        sb.AppendLine($"Found {list.Count} relevant matches:");

        foreach (var res in list)
        {
            sb.AppendLine();
            sb.AppendLine($"[{res.Doc.Category.ToUpper()}] RECOMMENDATION");

            var displayName = string.IsNullOrWhiteSpace(res.Doc.Title)
                ? res.Doc.FileName
                : res.Doc.Title;

            sb.AppendLine($"   Relevance Score : {res.DisplayScore:P2}");
            sb.AppendLine($"   Title           : {displayName}");
            sb.AppendLine($"   Source File     : {res.Doc.FileName}");
            sb.AppendLine("   --- Details ---");

            if (!string.IsNullOrWhiteSpace(res.Doc.Content))
            {
                var details = res.Doc.Content.Split(
                    new[] { '|', ';', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var detail in details)
                {
                    if (!string.IsNullOrWhiteSpace(detail))
                        sb.AppendLine($"   {detail.Trim()}");
                }
            }

            if (!string.IsNullOrWhiteSpace(res.Doc.Url))
                sb.AppendLine($"   Link            : {res.Doc.Url}");

            sb.AppendLine(new string('-', 60));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Derives a display label from a raw category name.
    /// E.g. "books" => "Books Recommendation", "restaurant" => "Restaurant Recommendation".
    /// </summary>
    private static string DeriveLabel(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "Recommendation";

        var titled = char.ToUpper(category[0]) + category[1..];
        return $"{titled} Recommendation";
    }
}