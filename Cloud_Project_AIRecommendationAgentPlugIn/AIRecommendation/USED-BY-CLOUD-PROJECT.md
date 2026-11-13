# What this library is used for in the Cloud project

`AIRecommendation` is referenced as a `ProjectReference` from `MyExperiment.Mcp.csproj`
(the MCP server). Nothing in `MyCloudProject` or `MyExperiment` references it directly —
they only talk to `MyExperiment.Mcp` over MCP (HTTP). This file lists exactly which types
from this library are consumed, by which file in `MyExperiment.Mcp`, and how.

## Consumed types

### Configuration (`Application/Configuration/ApplicationSettings.cs`)

| Type | Used in | How |
|---|---|---|
| `OpenAiSettings` | `Program.cs`, `Recommendationservice.cs` | Bound from the MCP server's own `"OpenAI"` appsettings section and passed into `OpenAiEmbeddingService` so query embeddings use the same model (`text-embedding-3-small`) as the stored document embeddings. |
| `RecommendationSettings` | `CosineSimilarityTool.cs` (wrapper) | Built on the fly from `McpConfig` (`MinScoreThreshold`, `ScoreDisplayCeiling`, `DefaultTopN`) and passed into the library's `CosineSimilarityTool.ExecuteAsync` so the ranking behaves the same as the rest of the server. |

### Interfaces (`Application/Interfaces/`)

| Type | Used in | How |
|---|---|---|
| `IEmbeddingService` | `Recommendationservice.cs` | The contract `OpenAiEmbeddingService` implements; `RecommendationService.EmbeddingService` is exposed as this type so all MCP tools embed text through one shared instance. |
| `IDocumentRepository` | `Recommendationservice.cs`, `IngestDocumentTool.cs` | The contract `EfDocumentRepository` implements; `RecommendationService.Repository` exposes it so tools can read/write documents without knowing about EF Core directly. |

### Infrastructure — Embeddings (`Infrastructure/Embeddings/OpenAiEmbeddingService.cs`)

- Used in `Recommendationservice.cs` — instantiated once per server lifetime (registered as
  a DI singleton via `RecommendationService`) and calls the real OpenAI embeddings API
  (`v1/embeddings`) to turn a natural-language query into a 1536-dim vector, matching the
  dimensionality of the vectors already stored in Azure SQL.
- `DummyEmbeddingService` (the 128-dim offline stand-in) is **not** used by
  `MyExperiment.Mcp` anymore — it was the original wiring and caused `Cosine.Similarity` to
  always return 0 (dimension mismatch) until it was swapped for `OpenAiEmbeddingService`.

### Infrastructure — Persistence (`Infrastructure/Persistence/`)

| Type | Used in | How |
|---|---|---|
| `AppDbContext` | `Recommendationservice.cs` | Instantiated directly with a `DbContextOptionsBuilder` pointed at `McpConfig.SqlConnectionString` (the Azure SQL DB, `s4.Documents` table). |
| `EfDocumentRepository` | `Recommendationservice.cs` | Wraps `AppDbContext` and is exposed as `IDocumentRepository` to the rest of the MCP server. |

### Infrastructure — Similarity (`Infrastructure/Similarity/Cosine.cs`)

- Used in `Recommendationservice.cs` and (via the delegated `CosineSimilarityTool`) in
  `CosineSimilarityTool.cs` — computes cosine similarity between the query vector and every
  stored document vector to rank recommendations. Returns exactly `0` on any vector-length
  mismatch, which is what originally exposed the Dummy-vs-OpenAI embedding bug.

### Infrastructure — Parsing (`Infrastructure/Parsing/`)

| Type | Used in | How |
|---|---|---|
| `TxtDocumentParser` | `IngestDocumentTool.cs` | Extracts plain text from an ingested `.txt` blob before it's embedded and stored. |
| `CsvRowsReader` | `IngestDocumentTool.cs` | Parses an ingested `.csv` blob into one dictionary-per-row so each row becomes its own separate, individually-searchable document. |
- `CsvDocumentParser`, `DaenetDocumentParser`, `IdBlockChunker` are **not** used by
  `MyExperiment.Mcp`.

### Domain (`Domain/Entities/DocumentRecord.cs`)

- Used throughout `IngestDocumentTool.cs` and `Recommendationservice.cs` — the row shape
  stored in Azure SQL (`Title`, `Content`, `EmbeddingJson`, `Category`, `FileName`, ...).

### Application — Tools (`Application/Tools/RecommendationTools.cs`)

| Type | Used in | How |
|---|---|---|
| `EmbeddingTool` | `EmbeddingTool.cs` (`EmbeddingTools` MCP tool) | Instantiated per-call with the shared `IEmbeddingService` and delegated to directly — this MCP tool is a thin wrapper, not a reimplementation. |
| `CosineSimilarityTool` | `CosineSimilarityTool.cs` (`CosineSimilarityTools` MCP tool) | Instantiated per-call with the shared `IEmbeddingService` + `IDocumentRepository` and delegated to directly, same as above. |

## Not consumed (dead code from the Cloud project's point of view)

These exist in `AIRecommendation` but nothing in the solution calls them:

- `Application/Services/RecommendationService.cs` (`RecommendationService`, `ScoredResult`) —
  superseded by `MyExperiment.Mcp/Recommendationservice.cs`, which reimplements the same
  query → embed → rank → filter logic independently (different response DTO,
  `McpRecommendResponse`, instead of `List<ScoredResult>`).
- `Application/Services/RecommendationFormatter.cs`
- `Application/Services/RecommendationExportService.cs`
- `Infrastructure/Embeddings/DummyEmbeddingService.cs`
- `Infrastructure/Parsing/CsvDocumentParser.cs`, `DaenetDocumentParser.cs`, `IdBlockChunker.cs`
- `Infrastructure/Persistence/AppDbContextFactory.cs`
- `Migrations/*`

## Net picture

`MyExperiment.Mcp` reuses this library for its lower layers (config models, interfaces,
OpenAI embedding client, EF Core persistence, cosine similarity, `.txt`/`.csv` parsing,
the `DocumentRecord` entity) and for two of its five MCP tools by direct delegation
(`EmbeddingTools`, `CosineSimilarityTools`). It does **not** reuse this library's own
`RecommendationService`/formatter/export classes — those were re-written locally inside
`MyExperiment.Mcp` instead, which is why they're currently dead code here.
