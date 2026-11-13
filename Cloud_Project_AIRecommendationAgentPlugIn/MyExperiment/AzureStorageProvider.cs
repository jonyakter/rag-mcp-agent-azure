// FILE: MyExperiment/AzureStorageProvider.cs
// ReceiveExperimentRequestAsync/CommitRequestAsync delegate to AzureQueueHandler, which
// reads the EXPERIMENT_INPUT environment variable instead of a real Azure Queue.
// DownloadInputAsync/UploadResultAsync delegate to Blob + Table as before.

using Microsoft.Extensions.Configuration;
using MyCloudProject.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyExperiment
{
    /// <summary>
    /// Azure Storage implementation of IStorageProvider.
    /// Blob (download + upload) and Table (result write) are used normally.
    /// The queue trigger is not a real Azure Queue — AzureQueueHandler reads the
    /// EXPERIMENT_INPUT environment variable instead.
    /// </summary>
    public class AzureStorageProvider : IStorageProvider
    {
        private readonly MyConfig _config;
        private readonly AzureBlobStorageProvider _blob;
        private readonly AzureTableStorage _table;
        private readonly AzureQueueHandler _queue;

        public AzureStorageProvider(IConfigurationSection configSection)
        {
            _config = new MyConfig();
            configSection.Bind(_config);

            _blob = new AzureBlobStorageProvider(
                _config.StorageConnectionString,
                _config.TrainingContainer,
                _config.ResultContainer);

            _table = new AzureTableStorage(
                _config.StorageConnectionString,
                _config.ResultTable);

            // No real Azure Queue in this architecture — AzureQueueHandler reads the
            // EXPERIMENT_INPUT environment variable instead. Params are accepted but ignored.
            _queue = new AzureQueueHandler(_config.StorageConnectionString, "unused");
        }

        // ── Active methods ────────────────────────────────────────────────────────────

        /// <summary>
        /// Downloads the input file from the Blob training container to a local temp file.
        /// Called by Program.cs with the blob name / URL provided at startup.
        /// </summary>
        public Task<string> DownloadInputAsync(string fileName)
            => _blob.DownloadInputAsync(fileName);

        /// <summary>
        /// Uploads result files to the Blob result container and writes
        /// the ExperimentResult row to Azure Table Storage.
        /// </summary>
        public async Task UploadResultAsync(string experimentName, IExperimentResult result)
        {
            if (result is ExperimentResult concrete && concrete.OutputFiles != null)
            {
                foreach (var localFile in concrete.OutputFiles)
                    await _blob.UploadResultFileAsync(experimentName, localFile);
            }

            await _table.WriteResultAsync(result);
        }

        // ── Trigger (env var, no real Azure Queue) ────────────────────────────────────

        /// <summary>
        /// Delivers one request built from the EXPERIMENT_INPUT environment variable,
        /// then null on every subsequent call so Program.cs's loop idles normally.
        /// </summary>
        public IExerimentRequest? ReceiveExperimentRequestAsync(CancellationToken token)
            => _queue.Receive(token);

        /// <summary>
        /// No-op — there is no queue message to delete in this architecture.
        /// </summary>
        public Task CommitRequestAsync(IExerimentRequest request)
            => _queue.CommitAsync(request);
    }
}