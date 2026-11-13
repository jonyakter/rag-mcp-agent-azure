using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using MyCloudProject.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HttpClientTransport = ModelContextProtocol.Client.HttpClientTransport;

namespace MyExperiment
{
    /// <summary>
    /// Thin client for calling the MyExperiment.Mcp server's tools (recommend, save,
    /// generate embedding, find similar documents, ingest document) over HTTP.
    /// </summary>
    public class McpClient : IAsyncDisposable
    {
        private readonly string _mcpServerUrl;

        public McpClient(string mcpServerUrl)
        {
            if (string.IsNullOrWhiteSpace(mcpServerUrl))
                throw new ArgumentException("MCP server URL is not configured.", nameof(mcpServerUrl));

            _mcpServerUrl = mcpServerUrl.TrimEnd('/');
        }

        /// <summary>
        /// Calls the "Recommend" tool on the MCP server.
        /// The Agent passes the query text — the MCP server handles embedding + cosine search.
        /// </summary>
        public async Task<McpRecommendResponse> FindSimilarAsync(
            float[] queryVector,      // not sent to MCP — MCP handles embedding internally
            string queryText,
            string? category = null,
            int topN = 5,
            CancellationToken token = default)
        {
            string responseJson = await CallToolAsync(
                "recommend",
                new Dictionary<string, object?>
                {
                    ["query"] = queryText ?? string.Empty,
                    ["category"] = category ?? string.Empty
                },
                token);

            if (string.IsNullOrWhiteSpace(responseJson))
                return new McpRecommendResponse { QueryText = queryText ?? string.Empty, FormattedText = string.Empty };

            try
            {
                return JsonSerializer.Deserialize<McpRecommendResponse>(responseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new McpRecommendResponse { QueryText = queryText ?? string.Empty, FormattedText = responseJson };
            }
            catch
            {
                // MCP returned plain text instead of JSON — wrap it
                return new McpRecommendResponse
                {
                    QueryText = queryText ?? string.Empty,
                    FormattedText = responseJson,
                    BestMatch = string.Empty,
                    BestScore = 0
                };
            }
        }

        /// <summary>
        /// Calls the "save_recommendation" tool — runs the recommendation and persists it as a
        /// JSON blob plus a Table Storage summary row on the MCP server. Returns the tool's
        /// formatted text (including the blob/table locations) for display in the console.
        /// </summary>
        public Task<string> SaveRecommendationAsync(
            string queryText, string category = "", CancellationToken token = default)
        {
            return CallToolAsync(
                "save_recommendation",
                new Dictionary<string, object?>
                {
                    ["query"] = queryText ?? string.Empty,
                    ["category"] = category ?? string.Empty
                },
                token);
        }

        /// <summary>
        /// Calls the "generate_embedding" tool — returns the embedding vector (dimensions,
        /// preview, and full JSON array) for a piece of text, as formatted text.
        /// </summary>
        public Task<string> GenerateEmbeddingAsync(string text, CancellationToken token = default)
        {
            return CallToolAsync(
                "generate_embedding",
                new Dictionary<string, object?> { ["query"] = text ?? string.Empty },
                token);
        }

        /// <summary>
        /// Calls the "find_similar_documents" tool — ranks stored documents by cosine
        /// similarity against the query, without persisting anything.
        /// </summary>
        public Task<string> FindSimilarDocumentsAsync(string query, CancellationToken token = default)
        {
            return CallToolAsync(
                "find_similar_documents",
                new Dictionary<string, object?> { ["query"] = query ?? string.Empty },
                token);
        }

        /// <summary>
        /// Calls the "ingest_document" tool — downloads a dataset blob already uploaded to the
        /// existing training-files container, parses/embeds it, and stores it in the SQL
        /// document store.
        /// </summary>
        public Task<string> IngestDocumentAsync(
            string blobName, string category = "", string title = "", CancellationToken token = default)
        {
            return CallToolAsync(
                "ingest_document",
                new Dictionary<string, object?>
                {
                    ["blobName"] = blobName ?? string.Empty,
                    ["category"] = category ?? string.Empty,
                    ["title"] = title ?? string.Empty
                },
                token);
        }

        /// <summary>
        /// Opens a connection to the MCP server and calls the named tool, returning its first
        /// text content block. Shared by FindSimilarAsync, SaveRecommendationAsync,
        /// GenerateEmbeddingAsync, FindSimilarDocumentsAsync, and IngestDocumentAsync — MCP SDK
        /// snake_cases the method name from the tool class (e.g. FindSimilarDocuments ->
        /// find_similar_documents) when generating the tool name.
        /// </summary>
        private async Task<string> CallToolAsync(
            string toolName, Dictionary<string, object?> arguments, CancellationToken token)
        {
            // MCP endpoint is at /mcp (registered by app.MapMcp() in MCP server Program.cs)
            var mcpEndpoint = new Uri(_mcpServerUrl + "/mcp");

            await using var client = await ModelContextProtocol.Client.McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = "RecommendationMCP",
                    Endpoint = mcpEndpoint
                }),
                cancellationToken: token);

            var result = await client.CallToolAsync(toolName, arguments, cancellationToken: token);

            return result?.Content
                .OfType<TextContentBlock>()
                .FirstOrDefault()?.Text
                ?? string.Empty;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}