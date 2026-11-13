namespace MyExperiment.Mcp
{
    /// <summary>
    /// Configuration for the MCP server, bound from the "McpConfig" section
    /// of the MCP server's appsettings.json / Azure App Service Application Settings.
    ///
    /// OpenAI is NOT configured here — the MCP server never calls OpenAI.
    /// OpenAI is called by the Agent (MyExperiment/Experiment.cs) before invoking MCP.
    /// </summary>
    public class McpConfig
    {
        /// <summary>
        /// Connection string of the Azure SQL database that holds document embeddings.
        /// </summary>
        public string SqlConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Azure Blob Storage connection string, used by SaveRecommendationTools to persist
        /// recommendation results as JSON. Set via env var McpConfig__StorageConnectionString —
        /// never commit a real value to appsettings.json.
        /// </summary>
        public string StorageConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Blob container that saved recommendation JSON files are uploaded to.
        /// Default: "result-files" (same container MyCloudProject's Agent writes results to).
        /// </summary>
        public string ResultContainer { get; set; } = "result-files";

        /// <summary>
        /// Blob container that source datasets are read from when ingesting a new document.
        /// Default: "training-files" — the same container MyCloudProject's Agent already uses
        /// for experiment input, reused here so no new Blob container is created.
        /// </summary>
        public string TrainingContainer { get; set; } = "training-files";

        /// <summary>
        /// Table Storage table that saved recommendation rows are written to.
        /// Default: "results" (same table MyCloudProject's Agent writes results to).
        /// </summary>
        public string ResultTable { get; set; } = "results";

        /// <summary>
        /// Minimum cosine similarity score for a result to be included.
        /// Default: 0.30
        /// </summary>
        public double MinScoreThreshold { get; set; } = 0.30;

        /// <summary>
        /// Ceiling used to scale raw cosine scores to a display percentage.
        /// Default: 0.60
        /// </summary>
        public double ScoreDisplayCeiling { get; set; } = 0.60;

        /// <summary>
        /// Default number of top results returned by FindSimilarDocuments.
        /// Default: 5
        /// </summary>
        public int DefaultTopN { get; set; } = 5;

        /// <summary>
        /// Maximum number of conversation turns MemoryRecallTool will search back through.
        /// Default: 10
        /// </summary>
        public int MaxRecallTurns { get; set; } = 10;
    }
}