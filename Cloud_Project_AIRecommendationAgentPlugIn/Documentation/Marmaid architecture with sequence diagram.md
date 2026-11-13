# AI Recommendation Agent — Architecture

## 1. System Architecture

```mermaid
flowchart TB
    Trigger(["Runner\n(EXPERIMENT_INPUT)"])
    User(["User"])

    subgraph Agent["Agent — MyCloudProject / MyExperiment"]
        Prog["Program.cs + Experiment.cs\n(batch mode)"]
        Chat["ChatSession\n(console REPL)"]
        McpClient["McpClient\n(MCP HTTP client)"]
        Prog --> McpClient
        Chat --> McpClient
    end

    subgraph MCPServer["MCP Server — MyExperiment.Mcp (ASP.NET Core, /mcp)"]
        Tools["MCP Tools\nrecommend / save_recommendation\ningest_document / generate_embedding\nfind_similar_documents"]
        RecSvc["RecommendationService"]
        Tools --> RecSvc
    end

    subgraph Lib["AIRecommendation (shared library)"]
        Embed["OpenAiEmbeddingService"]
        Repo["EfDocumentRepository"]
        Cos["Cosine.Similarity"]
        RecSvc --> Embed
        RecSvc --> Repo
        RecSvc --> Cos
    end

    OpenAI[["OpenAI API\n/v1/embeddings"]]
    SQL[("Azure SQL\nembeddingdb.s4.Documents")]
    Blob[("Azure Blob Storage\ntraining-files / result-files")]
    Table[("Azure Table Storage\nresults")]

    Trigger --> Prog
    User --> Chat
    Blob -- "download query.txt" --> Prog
    Chat -- "upload dataset" --> Blob
    McpClient -- "HTTP POST /mcp" --> Tools
    Embed --> OpenAI
    Repo --> SQL
    Tools -- "save_recommendation /\ningest_document" --> Blob
    Tools -- "save_recommendation" --> Table
    Prog -- "upload result file" --> Blob
    Prog -- "write ExperimentResult row" --> Table
```

**Notes**
- The Agent never calls OpenAI or Azure SQL directly — it only calls the MCP server over HTTP.
- The MCP server is the only component with access to OpenAI and Azure SQL.
- Two independent paths write results: batch mode persists from the **Agent** (`Program.cs`/`Experiment.cs`,
  via `IStorageProvider`); chat search persists from the **MCP server** (`save_recommendation` tool). The
  `recommend` tool used by batch mode does not persist anything itself.
- `AIRecommendation` is a shared class library, not a separate deployable service.
- Deployment: MCP server runs as an Azure Container App; images built to Azure Container Registry.

---

## 2. Data Flow Diagram

```mermaid
flowchart LR
    BT(["Runner"])
    U(["User"])
    OAI[["OpenAI\nEmbeddings API"]]

    P1(("Agent\nbatch orchestration"))
    P2(("MCP: recommend /\nsave_recommendation"))
    P3(("MCP: ingest_document"))

    D1[("D1\nBlob: training-files")]
    D2[("D2\nBlob: result-files")]
    D3[("D3\nTable: results")]
    D4[("D4\nSQL: s4.Documents")]

    BT -- "trigger" --> P1
    D1 -- "query.txt" --> P1
    P1 -- "query text" --> P2
    P2 -- "ranked results" --> P1
    P1 -- "result file" --> D2
    P1 -- "result row" --> D3

    U -- "search query" --> P2
    U -- "upload dataset" --> D1
    P2 -- "ranked results" --> U
    P2 -- "result JSON" --> D2
    P2 -- "result row" --> D3

    P2 -- "embed query" --> OAI
    OAI -- "query vector" --> P2
    P2 -- "read documents\n+ embeddings" --> D4

    D1 -- "raw file" --> P3
    P3 -- "embed rows" --> OAI
    P3 -- "delete + insert\ndocument + embedding" --> D4
```

---

## 3. End-to-End Sequence — Batch Mode

```mermaid
sequenceDiagram
    actor Trig as Runner
    participant Prog as Program.cs
    participant Blob1 as Blob (training-files)
    participant Exp as Experiment.cs
    participant Client as McpClient
    participant MCP as MCP Server (/mcp)
    participant Rec as RecommendationService
    participant OAI as OpenAI API
    participant SQL as Azure SQL (s4.Documents)
    participant Blob2 as Blob (result-files)
    participant Tbl as Table (results)

    Trig->>Prog: run (EXPERIMENT_INPUT=query.txt)
    Prog->>Blob1: download query.txt
    Blob1-->>Prog: query text
    Prog->>Exp: RunAsync(localFile)
    Exp->>Client: FindSimilarAsync(queryText)
    Client->>MCP: POST /mcp  tool=recommend
    MCP->>Rec: BuildRecommendationAsync(query)
    Rec->>OAI: POST /v1/embeddings (query)
    OAI-->>Rec: query embedding (1536-dim)
    Rec->>SQL: read documents + embeddings
    SQL-->>Rec: documents
    Rec->>Rec: Cosine.Similarity per document\nfilter (score >= 0.30), take top 5
    Rec-->>MCP: McpRecommendResponse
    MCP-->>Client: JSON response
    Client-->>Exp: McpRecommendResponse
    Exp->>Exp: write result_*.txt locally
    Exp-->>Prog: ExperimentResult
    Prog->>Blob2: upload result file
    Prog->>Tbl: write ExperimentResult row
    Prog-->>Trig: exit
```

---

## 4. End-to-End Sequence — Chat: Search / Recommend

```mermaid
sequenceDiagram
    actor User
    participant Chat as ChatSession (Agent)
    participant Client as McpClient
    participant MCP as MCP Server (/mcp)
    participant Rec as RecommendationService
    participant OAI as OpenAI API
    participant SQL as Azure SQL (s4.Documents)
    participant Blob as Blob (result-files)
    participant Tbl as Table (results)

    User->>Chat: select [1] Search, enter query
    Chat->>Client: SaveRecommendationAsync(query, category)
    Client->>MCP: POST /mcp  tool=save_recommendation
    MCP->>Rec: BuildRecommendationAsync(query, category)
    Rec->>OAI: POST /v1/embeddings (query)
    OAI-->>Rec: query embedding (1536-dim)
    Rec->>SQL: read documents + embeddings
    SQL-->>Rec: documents
    Rec->>Rec: Cosine.Similarity per document\nfilter (score >= 0.30), take top 5
    Rec-->>MCP: McpRecommendResponse
    MCP->>Blob: upload result JSON
    MCP->>Tbl: write result row
    MCP-->>Client: formatted text (incl. Blob/Table location)
    Client-->>Chat: formatted result text
    Chat-->>User: display ranked recommendations
```

---

## 5. End-to-End Sequence — Chat: Upload / Ingest Dataset

```mermaid
sequenceDiagram
    actor User
    participant Chat as ChatSession (Agent)
    participant Blob as Blob (training-files)
    participant Client as McpClient
    participant MCP as MCP Server (/mcp)
    participant Parse as CsvRowsReader / TxtDocumentParser
    participant OAI as OpenAI API
    participant SQL as Azure SQL (s4.Documents)

    User->>Chat: select [2], enter local file path (.txt / .csv)
    Chat->>Blob: upload file to training-files
    Chat->>Client: IngestDocumentAsync(blobName, category)
    Client->>MCP: POST /mcp  tool=ingest_document
    MCP->>Blob: download file
    MCP->>Parse: parse rows (csv) / extract text (txt)
    MCP->>SQL: delete existing rows for blobName (idempotent re-ingest)
    loop each CSV row (or the whole .txt document)
        MCP->>OAI: POST /v1/embeddings
        OAI-->>MCP: embedding vector
        MCP->>SQL: add document + embedding
    end
    MCP->>SQL: save changes
    MCP-->>Client: ingestion summary
    Client-->>Chat: confirmation
    Chat-->>User: display ingestion result
```
