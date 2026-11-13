// FILE: MyExperiment/MyConfig.cs
// CHANGE:
//   ADDED:   OpenAiApiKey  — the Agent calls OpenAI, so the key lives here (not in McpConfig).
//   ADDED:   OpenAiModel   — embedding model name (default: text-embedding-3-small).
//   REMOVED: Queue         — no Azure Queue in this architecture (professor's instruction).
//   KEPT:    StorageConnectionString, TrainingContainer, ResultContainer, ResultTable,
//            GroupId, McpServerUrl, TopN.

namespace MyExperiment
{
    /// <summary>
    /// Configuration for the Agent (MyCloudProject / MyExperiment).
    /// Bound from the "MyConfig" section of appsettings.json.
    ///
    /// In Azure: set these as Application Settings (or environment variables) on the
    /// Container App / App Service that runs MyCloudProject.
    /// </summary>
    public class MyConfig
    {
        // ── Azure Storage ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Azure Storage connection string.
        /// Used for Blob (download input / upload result) and Table (log result row).
        /// </summary>
        public string StorageConnectionString { get; set; } = string.Empty;

        /// <summary>Blob container name where training/input files are stored.</summary>
        public string TrainingContainer { get; set; } = string.Empty;

        /// <summary>Blob container name where experiment result files are uploaded.</summary>
        public string ResultContainer { get; set; } = string.Empty;

        /// <summary>Azure Table name where ExperimentResult rows are written.</summary>
        public string ResultTable { get; set; } = string.Empty;

        // ── Group / experiment identity ───────────────────────────────────────────────

        /// <summary>Your group name — used as PartitionKey in Table Storage.</summary>
        public string GroupId { get; set; } = string.Empty;

        // ── MCP Server ────────────────────────────────────────────────────────────────

        /// <summary>
        /// URL of the MCP server (MyExperiment.Mcp) deployed on Azure App Service.
        /// Example: https://my-mcp-server.azurewebsites.net
        /// The Agent connects here to perform cosine similarity search.
        /// </summary>
        public string McpServerUrl { get; set; } = string.Empty;

        /// <summary>Number of top recommendations to request from the MCP server. Default: 5.</summary>
        public int TopN { get; set; } = 5;

        // ── OpenAI (Agent owns this — NOT the MCP server) ─────────────────────────────

        /// <summary>
        /// OpenAI API key used by the Agent to embed the query before calling the MCP server.
        /// The MCP server (McpConfig) does NOT have this key — it never calls OpenAI.
        ///
        /// Set as an environment variable: MyConfig__OpenAiApiKey=sk-xxxx
        /// Never commit the real key to source control.
        /// </summary>
        public string OpenAiApiKey { get; set; } = string.Empty;

        /// <summary>
        /// OpenAI embedding model name used by the Agent.
        /// Must match the model used when the DB embeddings were generated (ImporterApp).
        /// Default: text-embedding-3-small (1536 dimensions).
        /// </summary>
        public string OpenAiModel { get; set; } = "text-embedding-3-small";

        // NOTE: Queue property removed.
        // There is no Azure Queue in this architecture. The Agent is triggered directly
        // (e.g. via environment variable / command-line argument / HTTP trigger),
        // and calls the MCP server over HTTP without any queue in between.
    }
}