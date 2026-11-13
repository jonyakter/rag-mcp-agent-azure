# AI Recommendation Agent Plugin - Azure Cloud Implementation

This file documents our Azure cloud experiment. Diagrams referenced below are stored under `/Documentation`
and linked with relative paths — see [`architecture.png`](architecture.png) and
[`processing_flow.png`](processing_flow.png) for the rendered diagrams, [`Marmaid architecture with
sequence diagram.md`](Marmaid%20architecture%20with%20sequence%20diagram.md) for the underlying Mermaid
source (system architecture, data flow, and three end-to-end sequence diagrams — batch mode plus both
chat scenarios), and
[`../README.md`](../README.md) for the project overview.

## What is your experiment about

Our experiment is a **semantic recommendation agent**. It recommends the most relevant movies for a user's
natural-language query by comparing the query's embedding against stored movie embeddings using **cosine
similarity**, then returning the highest-scoring matches as a ranked list.

The recommendation logic was developed in our Software Engineering project and is reused here unchanged,
wrapped as MCP (Model Context Protocol) tools. The cloud experiment is split into two deployable pieces:

- **Agent** (`MyCloudProject` + `MyExperiment`) — a console app that resolves the query, calls the MCP
  server, and persists the result. It contains no recommendation logic.
- **MCP server** (`MyExperiment.Mcp`) — an ASP.NET Core service, deployed as an Azure Container App, that
  owns the entire recommendation process: embedding the query, ranking stored movies by cosine similarity,
  and formatting the response. It is the only component that talks to OpenAI and Azure SQL.

1. **What is the input?**

   - A user **query** (a natural-language request such as `"find a horror movie"` or `"a feel-good
     science fiction movie from the 1980s"`).
   - The query is provided as a plain-text file. Its blob name is given by the `EXPERIMENT_INPUT`
     environment variable (falling back to `query.txt` if unset), and the file itself is downloaded from
     the `training-files` Blob container before the run starts.
   - There is **no Azure Storage Queue** in the current architecture — the original queue-based trigger
     from the course sample was replaced with a direct environment-variable trigger, since each experiment
     run corresponds to a single, already-known query rather than a stream of incoming requests.

2. **What is the output?**

   - A **result text file** uploaded to the `result-files` Blob container, containing the formatted,
     human-readable ranked recommendation list.
   - A **result row** written to Azure Table Storage (table `results`) with the run metadata (experiment
     id, query, best match, similarity score, timing, duration).
   - Separately, the MCP server also exposes a `save_recommendation` tool that runs the same process and
     persists a full JSON result to Blob plus a summary row to the same `results` table — used when
     querying the MCP server directly (e.g. from an MCP client) without going through the Agent.

3. **What does your algorithm do? How?**

   The algorithm is embedding-based semantic similarity matching, in two stages:

   - **Indexing (build the knowledge base, done ahead of time):** Each movie's descriptive text (title,
     genres, director, cast, plot summary) is embedded and stored, together with the movie fields, in
     Azure SQL Database (`s4.Documents` — see the schema note below).
   - **Recommendation (per query, done by the MCP server on every call):** The user query is embedded with
     OpenAI's `text-embedding-3-small` model (1536 dimensions — must match the dimensionality the documents
     were indexed with, or cosine similarity silently returns 0 for every comparison). Cosine similarity is
     computed between the query vector and every stored movie vector. Movies scoring above a minimum
     threshold (0.30) are kept, ranked by score in descending order, and the top-N (default 5) are
     returned. The results are formatted into a readable, structured recommendation list. There is no
     text-generation step — recommendations come directly from the similarity ranking.

## How to run experiment

The experiment does not poll a queue. `MyCloudProject` has two entry points, both sharing the same
downstream Azure data layer (SQL, Blob, Table) — see `processing_flow.png` for the diagram:

- **Batch mode** (no CLI args — `dotnet run --project MyCloudProject`, used by the graded cloud process):
  the Agent runs once per invocation. It downloads the query file named by `EXPERIMENT_INPUT` from Blob
  Storage, calls the MCP server over HTTP (`POST {McpServerUrl}/mcp`, tool `recommend`), writes the result
  to a local text file, uploads it to `result-files`, and writes an `ExperimentResult` row to Table
  Storage. A successful console run is shown in `Results/Results for batch mode.jpg`.
- **Interactive chat mode** (`dotnet run --project MyCloudProject -- chat`): a console REPL against the
  same MCP server. `[1] Search` calls the MCP `save_recommendation` tool (embed → rank by cosine similarity
  → save to Blob + Table → display); `[2] Upload a new dataset` uploads a local `.txt`/`.csv` file to
  `training-files` and calls `ingest_document` to embed and store it in Azure SQL. This mode was used for
  the manual, step-by-step verification captured in `Results/Results_console1.jpg` through
  `Results_console3.jpg`.

**_Trigger: the `EXPERIMENT_INPUT` environment variable (replaces the queue JSON message)_**

```bash
EXPERIMENT_INPUT=query.txt
```

- `EXPERIMENT_INPUT` : the blob name of the query file (in `training-files`) to download and use as the
  query for this run. Falls back to `query.txt` if not set.

The MCP tool call the Agent makes once the query text is resolved is equivalent to:

```json
{
  "tool": "recommend",
  "arguments": {
    "query": "find a horror movie",
    "category": ""
  }
}
```

- `query` : the user's natural-language recommendation request (the full contents of the downloaded file).
- `category` : optional filter (`movies`, `books`, `restaurant`); empty means all categories.

**_Describe your blob container registry:_**

Two blob containers are used:

- `training-files` : the query input file for each run.
  - `query.txt` (or whatever blob name `EXPERIMENT_INPUT` points to) — plain text containing the
    natural-language query.
- `result-files` : the output written by each run.
  - `{ExperimentName}_result_{yyyyMMdd_HHmmss}.txt` — the Agent's formatted recommendation list for that
    run.
  - `mcp-{yyyyMMdd_HHmmss}-{shortGuid}.json` — the full JSON result, written directly by the MCP server's
    `save_recommendation` tool when called outside the Agent flow.

**_Describe the Result Table_**

There is **one** result table (named `results`), populated from two different call paths that both write
rows into it with slightly different columns:

- Agent runs (`ExperimentResult`, via `MyExperiment/AzureTableStorage.cs`) — `PartitionKey` is the group
  id (`RG_Insight_Agent`), `RowKey` is a per-run GUID.
- Direct `save_recommendation` calls (via `MyExperiment.Mcp/SaveRecommendationTool.cs`) — `PartitionKey`
  is the run date (`yyyyMMdd`), `RowKey` is a per-run GUID.

Screenshots of the table and blob results, taken directly from the Azure Portal, are included in
`Documentation/Results/table storage with results files.jpg` and
`Documentation/Results/blob storage with results files.jpg`.

Column meanings (Agent-run rows):

- `PartitionKey` : group id (`RG_Insight_Agent`).
- `RowKey` : unique id of the run (GUID).
- `ExperimentId` : id of the experiment run (GUID).
- `Name` : experiment display name (`AI Movie Recommendation`).
- `Description` : `"Query: {query} | Best match: {bestTitle}"`.
- `InputFileUrl` : local temp path the query file was downloaded to for this run.
- `OutputFiles` : local path of the written result text file (semicolon-joined if more than one).
- `Accuracy` : the top match's cosine similarity score.
- `StartTimeUtc` / `EndTimeUtc` : when the run started/finished.
- `DurationSec` : total run duration in seconds.

Column meanings (`save_recommendation` rows):

- `PartitionKey` : run date (`yyyyMMdd`).
- `RowKey` : unique id of the run (GUID).
- `ExperimentId` : same GUID as `RowKey`.
- `Query` : the user query that was processed.
- `BestMatchingMovie` : title of the top-ranked movie.
- `SimilarityScore` : cosine score of the top match.
- `ResultFileUrl` : Blob URL of the full JSON result file.
- `StartTimeUtc` / `EndTimeUtc` / `DurationSec` : as above.

Some columns are obligatory for `ITableEntity` and need no explanation (for example `Timestamp`, `ETag`).

Example result JSON (uploaded to `result-files` by `save_recommendation`):

```json
{
  "QueryText": "a romantic comedy",
  "BestMatch": "Manhattan",
  "BestScore": 0.5081053485234042,
  "FormattedText": "Recommendations for: \"a romantic comedy\"\n\n1. Manhattan (match: 85%)\n2. Roman Holiday (match: 84%)\n3. (500) Days of Summer (match: 83%)\n4. True Romance (match: 82%)\n5. The Apartment (match: 82%)"
}
```

Example result table row (queried directly from Azure via `az storage entity show`, verified working
end-to-end):

```json
{
  "PartitionKey": "20260821",
  "RowKey": "0437c0fb-47a2-46af-b32b-f3cdfcc3cbe8",
  "ExperimentId": "0437c0fb-47a2-46af-b32b-f3cdfcc3cbe8",
  "Query": "a romantic comedy",
  "BestMatchingMovie": "Manhattan",
  "SimilarityScore": 0.5081053485234042,
  "ResultFileUrl": "https://blobstorinsite.blob.core.windows.net/result-files/mcp-20260821_171444-6dd495b4.json",
  "StartTimeUtc": "2026-08-21T17:14:35.558905+00:00",
  "EndTimeUtc": "2026-08-21T17:14:44.368969+00:00",
  "DurationSec": 8
}
```

## Verification / screenshots

All screenshots referenced above (and below) live under `Documentation/Results/`. Together they document
one full pass through the deployed system, from provisioned resources down to the persisted result rows:

- `Resource group with container, storage.jpg` — the Azure resource group holding the Container App, the
  Container Registry, the Storage account, and the SQL Database used by this experiment.
- `Mcp server running in cloud.jpg`, `Mcp server running with no logs.jpg` — the MCP server running as the
  Azure Container App (`insightagent-mcp-server`, `germanywestcentral`).
- `log workspace.jpg` — the Log Analytics workspace attached to the Container App.
- `blob storage with training files.jpg`, `blob storage with results files.jpg` — the `training-files` and
  `result-files` Blob containers after a run.
- `table storage with results files.jpg`, `Results in blob storagecontainer.jpg` — the `results` Table
  Storage rows and the corresponding JSON result blob.
- `Results_console1.jpg` → `Results_console3.jpg` — the interactive **chat mode** walkthrough (search +
  dataset upload) run against the deployed MCP server.
- `Results for batch mode.jpg` — a **batch mode** run (`dotnet run --project MyCloudProject`, no CLI args)
  completing end-to-end against the deployed MCP server and live Azure Storage.
