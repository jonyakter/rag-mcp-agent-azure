namespace AIRecommendation.Application.Configuration;

/// <summary>
/// Root application configuration model bound from appsettings.json.
/// This is the single source of truth for all application settings.
/// Inject via IOptions&lt;ApplicationSettings&gt; to access the entire configuration.
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// Gets or sets the database connection strings.
    /// </summary>
    public ConnectionStringsSettings ConnectionStrings { get; set; } = new();

    /// <summary>
    /// Gets or sets the OpenAI configuration settings.
    /// </summary>
    public OpenAiSettings OpenAi { get; set; } = new();

    /// <summary>
    /// Gets or sets the import service configuration.
    /// </summary>
    public ImportSettings Import { get; set; } = new();

    /// <summary>
    /// Gets or sets the recommendation engine settings.
    /// </summary>
    public RecommendationSettings Recommendation { get; set; } = new();

    /// <summary>
    /// Gets or sets the conversation memory settings.
    /// </summary>
    public MemorySettings Memory { get; set; } = new();

    /// <summary>
    /// Gets or sets the Tesseract OCR settings.
    /// </summary>
    public TessDataSettings TessData { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of dataset profiles used for category detection,
    /// title extraction, and CSV delimiter selection.
    /// Replaces all hardcoded category/keyword if-else chains.
    /// </summary>
    public List<DatasetProfile> DatasetProfiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the recommendation result export settings.
    /// </summary>
    public ExportSettings Export { get; set; } = new();
}

/// <summary>
/// Database connection string configuration.
/// </summary>
public class ConnectionStringsSettings
{
    /// <summary>
    /// Gets or sets the default SQL Server connection string.
    /// </summary>
    public string Default { get; set; } = string.Empty;
}

/// <summary>
/// OpenAI API configuration.
/// Typed configuration model bound from the "OpenAI" section in appsettings.json.
/// Injected via IOptions&lt;OpenAiSettings&gt; into services that need the API key or model.
/// </summary>
public class OpenAiSettings
{
    /// <summary>
    /// Gets or sets the OpenAI API key.
    /// Set via environment variable: OpenAI__ApiKey = sk-xxxx or OPENAI__APIKEY
    /// Leave empty in appsettings.json - never commit a real key to source control.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenAI embedding model name.
    /// Set in appsettings.json → OpenAI:Model
    /// Example: "text-embedding-3-small"
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Gets or sets the relative URL path for the embeddings endpoint.
    /// Default: "v1/embeddings"
    /// </summary>
    public string EmbeddingsPath { get; set; } = "v1/embeddings";
}

/// <summary>
/// Import service configuration settings.
/// </summary>
public class ImportSettings
{
    /// <summary>
    /// Gets or sets the relative data folder path from the application base directory.
    /// Default: "data"
    /// </summary>
    public string DataFolderPath { get; set; } = "data";

    /// <summary>
    /// Gets or sets a value indicating whether to clear the database before importing.
    /// Default: true
    /// </summary>
    public bool ClearDatabaseOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of characters per text chunk.
    /// Chunks longer than this are truncated before embedding.
    /// Default: 4000
    /// </summary>
    public int MaxChunkLength { get; set; } = 4000;
}

/// <summary>
/// Recommendation engine settings — controls search quality thresholds and result counts.
/// Bound from the "Recommendation" section in appsettings.json.
/// </summary>
public class RecommendationSettings
{
    /// <summary>
    /// Gets or sets the minimum cosine similarity score for a result to be included.
    /// Range: 0.0 – 1.0. Default: 0.30
    /// </summary>
    public double MinScoreThreshold { get; set; } = 0.30;

    /// <summary>
    /// Gets or sets the default number of results to return when the user does not specify.
    /// Default: 5
    /// </summary>
    public int DefaultTopN { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of results a user can request.
    /// Default: 20
    /// </summary>
    public int MaxTopN { get; set; } = 20;

    /// <summary>
    /// Gets or sets the minimum number of results a user can request.
    /// Default: 1
    /// </summary>
    public int MinTopN { get; set; } = 1;

    /// <summary>
    /// Gets or sets the practical ceiling for raw cosine similarity scores produced by the
    /// configured embedding model. Even a strongly relevant match rarely scores close to 1.0,
    /// so displayed scores are divided by this ceiling (and clamped to 100%) to turn a raw
    /// score like 0.43 into a more representative displayed percentage. This only affects the
    /// score shown to the user/exported to JSON — MinScoreThreshold filtering and ranking
    /// still use the raw, unscaled cosine score. Default: 0.60
    /// </summary>
    public double ScoreDisplayCeiling { get; set; } = 0.60;
}

/// <summary>
/// Conversation memory settings.
/// Bound from the "Memory" section in appsettings.json.
/// </summary>
public class MemorySettings
{
    /// <summary>
    /// Gets or sets the maximum number of recent conversation turns
    /// to include in a summary recall response.
    /// Default: 10
    /// </summary>
    public int MaxRecallTurns { get; set; } = 10;
}

/// <summary>
/// Tesseract OCR configuration settings.
/// Bound from the "TessData" section in appsettings.json.
/// </summary>
public class TessDataSettings
{
    /// <summary>
    /// Gets or sets the path to the tessdata directory containing trained models.
    /// Default: "tessdata"
    /// </summary>
    public string TessDataDir { get; set; } = "tessdata";

    /// <summary>
    /// Gets or sets the Tesseract OCR language code.
    /// Example: "eng" for English, "deu" for German.
    /// Default: "eng"
    /// </summary>
    public string Language { get; set; } = "eng";
}

/// <summary>
/// Recommendation result export settings.
/// Bound from the "Export" section in appsettings.json.
/// </summary>
public class ExportSettings
{
    /// <summary>
    /// Gets or sets the folder (relative to the solution root, or absolute) where
    /// recommendation result JSON files are written.
    /// Default: "RecommendationResults"
    /// </summary>
    public string OutputFolderPath { get; set; } = "RecommendationResults";
}

/// <summary>
/// Describes a dataset domain: how to detect its category from filenames or queries,
/// which CSV columns hold the item title, and which delimiter its CSV files use.
/// All category-specific hardcoded strings are replaced by this profile.
/// </summary>
public class DatasetProfile
{
    /// <summary>
    /// Gets or sets the canonical category name stored in the database (e.g. "books").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable label shown in recommendation output.
    /// Example: "Book Recommendation"
    /// </summary>
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets substrings that, when found in a filename (case-insensitive),
    /// identify this category.
    /// </summary>
    public List<string> FileNameKeywords { get; set; } = new();

    /// <summary>
    /// Gets or sets substrings that, when found in a user query (case-insensitive),
    /// identify this category.
    /// </summary>
    public List<string> QueryKeywords { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordered list of CSV column names to try when extracting
    /// the item title. The first non-empty match is used.
    /// </summary>
    public List<string> TitleColumns { get; set; } = new();

    /// <summary>
    /// Gets or sets the CSV column delimiter character used by this dataset's files.
    /// Default: ","
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Gets or sets the ordered list of CSV column names whose values are concatenated to
    /// build the text that gets embedded for semantic search. Restricting the embedded text
    /// to meaning-bearing columns (title, genre, description, etc.) instead of every column
    /// (URLs, run times, vote counts, ...) keeps the embedding focused on what a query is
    /// actually about, which raises cosine similarity for genuinely relevant matches.
    /// Columns not present in a given row are skipped. When empty, falls back to embedding
    /// every column in the row (legacy behavior).
    /// </summary>
    public List<string> SemanticColumns { get; set; } = new();
}
