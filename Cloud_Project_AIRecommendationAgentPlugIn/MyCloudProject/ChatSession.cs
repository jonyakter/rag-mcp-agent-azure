using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using MyExperiment;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyCloudProject
{
    /// <summary>
    /// Interactive console loop for the Agent. On each turn it asks whether the user wants to
    /// search for a recommendation or upload a new dataset, then:
    ///   - Search: sends the query to the MCP "save_recommendation" tool. Result is shown here
    ///     and persisted by the MCP server (Blob JSON + Table Storage row).
    ///   - Upload: uploads the local file to the EXISTING training-files Blob container (the
    ///     same one used for experiment input — no new container is created), then calls the
    ///     MCP "ingest_document" tool, which parses/embeds the file and stores it in Azure SQL.
    /// The Agent never talks to OpenAI or Azure SQL directly — only the MCP server does.
    /// Advanced commands (not required for the guided flow, kept for debugging):
    ///   /preview &lt;query&gt;, /similar &lt;query&gt;, /embed &lt;text&gt;, /help, exit/quit/bye.
    /// </summary>
    public sealed class ChatSession
    {
        private readonly MyConfig _config;
        private readonly McpClient _mcpClient;
        private readonly ILogger _logger;

        private static readonly string[] ExitWords = { "/exit", "/quit", "exit", "quit", "bye", "goodbye" };

        public ChatSession(MyConfig config, McpClient mcpClient, ILogger logger)
        {
            _config = config;
            _mcpClient = mcpClient;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Console.WriteLine("Insight Agent - interactive console (connected to the MCP server)");

            while (!ct.IsCancellationRequested)
            {
                Console.WriteLine();
                Console.WriteLine("What would you like to do?");
                Console.WriteLine("  [1] Search for a movie recommendation");
                Console.WriteLine("  [2] Upload a new dataset");
                Console.WriteLine("  (or type 'exit' to quit, '/help' for advanced commands)");
                Console.Write("you> ");

                string? choice = Console.ReadLine()?.Trim();
                if (choice is null || IsExitCommand(choice))
                    break;

                if (choice.Length == 0)
                    continue;

                try
                {
                    if (choice.Equals("/help", StringComparison.OrdinalIgnoreCase))
                    {
                        PrintAdvancedHelp();
                    }
                    else if (IsSearchChoice(choice))
                    {
                        await HandleSearchAsync(ct);
                    }
                    else if (IsUploadChoice(choice))
                    {
                        await HandleUploadAsync(ct);
                    }
                    else if (choice.StartsWith("/preview ", StringComparison.OrdinalIgnoreCase))
                    {
                        var r = await _mcpClient.FindSimilarAsync(
                            Array.Empty<float>(), choice["/preview ".Length..].Trim(), token: ct);
                        Console.WriteLine(r.FormattedText);
                    }
                    else if (choice.StartsWith("/similar ", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(await _mcpClient.FindSimilarDocumentsAsync(
                            choice["/similar ".Length..].Trim(), ct));
                    }
                    else if (choice.StartsWith("/embed ", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(await _mcpClient.GenerateEmbeddingAsync(
                            choice["/embed ".Length..].Trim(), ct));
                    }
                    else
                    {
                        // Fall back to treating free-form text as a recommendation query directly.
                        Console.WriteLine("Thinking...");
                        Console.WriteLine(await _mcpClient.SaveRecommendationAsync(choice, category: "", token: ct));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Chat command failed: {Line}", choice);
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            Console.WriteLine("Goodbye.");
        }

        private async Task HandleSearchAsync(CancellationToken ct)
        {
            Console.Write("Enter your recommendation query> ");
            string? query = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(query) || IsExitCommand(query))
                return;

            Console.WriteLine("Thinking...");
            string result = await _mcpClient.SaveRecommendationAsync(query, category: "", token: ct);
            Console.WriteLine(result);
        }

        private async Task HandleUploadAsync(CancellationToken ct)
        {
            Console.Write("Enter the local file path (.txt or .csv)> ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || IsExitCommand(path))
                return;

            if (!File.Exists(path))
            {
                Console.WriteLine($"File not found: {path}");
                return;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".txt" && extension != ".csv")
            {
                Console.WriteLine($"Unsupported file type '{extension}'. Only .txt and .csv are supported.");
                return;
            }

            Console.Write("Optional category (press Enter to skip)> ");
            string category = Console.ReadLine()?.Trim() ?? "";

            string blobName = Path.GetFileName(path);
            var container = new BlobContainerClient(_config.StorageConnectionString, _config.TrainingContainer);
            await container.CreateIfNotExistsAsync();
            await container.GetBlobClient(blobName).UploadAsync(path, overwrite: true);
            Console.WriteLine($"Uploaded '{path}' to blob '{_config.TrainingContainer}/{blobName}'.");

            Console.WriteLine("Embedding and storing in SQL via MCP server...");
            string result = await _mcpClient.IngestDocumentAsync(blobName, category, token: ct);
            Console.WriteLine(result);
        }

        private static bool IsSearchChoice(string s) =>
            s == "1" || s.Equals("search", StringComparison.OrdinalIgnoreCase)
                     || s.Equals("recommend", StringComparison.OrdinalIgnoreCase);

        private static bool IsUploadChoice(string s) =>
            s == "2" || s.Equals("upload", StringComparison.OrdinalIgnoreCase)
                     || s.Equals("dataset", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("dataset", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("upload", StringComparison.OrdinalIgnoreCase);

        private static bool IsExitCommand(string line) =>
            Array.Exists(ExitWords, w => line.Equals(w, StringComparison.OrdinalIgnoreCase));

        private static void PrintAdvancedHelp()
        {
            Console.WriteLine("Advanced commands:");
            Console.WriteLine("  /preview <query>   Recommendation ranking, shown but not saved");
            Console.WriteLine("  /similar <query>   Cosine-similarity search, not saved");
            Console.WriteLine("  /embed <text>      Show the embedding vector for some text");
            Console.WriteLine("  exit / quit / bye  Quit");
        }
    }
}
