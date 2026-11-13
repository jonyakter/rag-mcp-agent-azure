using MyCloudProject.Common;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Threading;
using MyExperiment;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Azure.Storage.Queues;
using System.Text.Json;
using System.Text;
using System.Linq;
using System.IO;

namespace MyCloudProject
{
    /// <summary>
    /// Entry point for the cloud experiment console app. Supports two modes:
    /// batch mode (default — downloads input from Blob, runs the experiment via MCP, uploads
    /// the result) and interactive "chat" mode (started with the "chat" argument).
    /// Shows "0 references" in the IDE because the .NET runtime invokes Main directly at
    /// process startup — no code in this project calls Program itself.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Your project ID from the last semester.
        /// </summary>
        private static string _projectName = "ML 25/26-AIRecommendationAgent";

        static async Task Main(string[] args)
        {
            CancellationTokenSource tokeSrc = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokeSrc.Cancel();
            };

            Console.WriteLine($"Started experiment: {_projectName}");

            // Init configuration
            var cfgRoot = Common.InitHelpers.InitConfiguration(args);

            var cfgSec = cfgRoot.GetSection("MyConfig");

            // InitLogging
            var logFactory = InitHelpers.InitLogging(cfgRoot);

            ILogger logger = logFactory.CreateLogger("Train.Console") ?? NullLogger.Instance;

            logger.LogInformation($"{DateTime.Now} -  Started experiment: {_projectName}");

            // "chat" arg -> interactive console mode instead of the one-shot batch run below.
            // Batch mode (no args / EXPERIMENT_INPUT) stays unchanged for the graded pipeline.
            if (args.Any(a => a.Equals("chat", StringComparison.OrdinalIgnoreCase)))
            {
                var chatConfig = new MyConfig();
                cfgSec.Bind(chatConfig);

                await using var chatMcpClient = new McpClient(chatConfig.McpServerUrl);
                var chat = new ChatSession(chatConfig, chatMcpClient, logger);
                await chat.RunAsync(tokeSrc.Token);
                return;
            }

            IStorageProvider storageProvider = new AzureStorageProvider(cfgSec);

            IExperiment experiment = new Experiment(cfgSec, storageProvider, logger/* put some additional config here */);

            //
            // Implements the step 3 in the architecture picture.

            try
            {
                // Step 4 - Download input file from Blob
                string inputFile = Environment.GetEnvironmentVariable("EXPERIMENT_INPUT") ?? "query.txt";
                logger?.LogInformation($"{DateTime.Now} - Downloading input file: {inputFile}");
                var localFileWithInputArgs = await storageProvider.DownloadInputAsync(inputFile);
                logger?.LogInformation($"{DateTime.Now} - Download complete. Local path: {localFileWithInputArgs}");
                Console.WriteLine($"[Batch] Downloaded '{inputFile}' from Blob Storage (container: {cfgSec["TrainingContainer"]}) -> {localFileWithInputArgs}");

                // Step 4→5 - Run the experiment (calls MCP server internally)
                logger?.LogInformation($"{DateTime.Now} - Running experiment...");
                IExperimentResult result = await experiment.RunAsync(localFileWithInputArgs);
                logger?.LogInformation($"{DateTime.Now} - Experiment complete.");

                // Step 5 - Upload result to Blob + Table
                logger?.LogInformation($"{DateTime.Now} - Uploading result...");
                await storageProvider.UploadResultAsync("outputfile", result);
                logger?.LogInformation($"{DateTime.Now} - Result uploaded successfully.");

                if (result is MyExperiment.ExperimentResult concreteResult && concreteResult.OutputFiles?.Length > 0)
                {
                    string resultBlobName = $"outputfile_{Path.GetFileName(concreteResult.OutputFiles[0])}";
                    Console.WriteLine($"[Batch] Uploaded result to Blob Storage (container: {cfgSec["ResultContainer"]}) as '{resultBlobName}', and wrote row to Table '{cfgSec["ResultTable"]}'.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, $"{DateTime.Now} - ERROR during experiment: {ex.Message}");
            }
            logger?.LogInformation($"{DateTime.Now} -  Experiment exit: {_projectName}");
        }


    }
}
