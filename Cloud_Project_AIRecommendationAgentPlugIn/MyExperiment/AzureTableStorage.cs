using Azure.Data.Tables;
using MyCloudProject.Common;
using System;
using System.Threading.Tasks;

namespace MyExperiment
{
    /// <summary>
    /// Handles writing the experiment result record to Azure Table Storage.
    /// A flexible TableEntity is used so that only Table-supported types are stored
    /// (the result contains an array and a TimeSpan, which Table Storage does not
    /// support directly).
    /// </summary>
    public class AzureTableStorage
    {
        private readonly TableClient _resultTable;

        public AzureTableStorage(string connectionString, string tableName)
        {
            _resultTable = new TableClient(connectionString, tableName);
            _resultTable.CreateIfNotExists();
        }

        /// <summary>
        /// Writes the result to Table Storage.
        /// </summary>
        public async Task WriteResultAsync(IExperimentResult result)
        {
            if (result is not ExperimentResult r)
                return;

            var entity = new TableEntity(r.PartitionKey, r.RowKey)
            {
                { "ExperimentId", r.ExperimentId },
                { "Name", r.Name },
                { "Description", r.Description },
                { "InputFileUrl", r.InputFileUrl },
                { "OutputFiles", r.OutputFiles != null ? string.Join(";", r.OutputFiles) : string.Empty },
                { "Accuracy", (double)r.Accuracy },
                { "DurationSec", r.DurationSec },
                { "StartTimeUtc", ToUtc(r.StartTimeUtc) },
                { "EndTimeUtc", ToUtc(r.EndTimeUtc) }
            };

            await _resultTable.UpsertEntityAsync(entity);
        }
        /// <summary>
        /// Table Storage requires DateTime values to be UTC. Returns a UTC DateTimeOffset
        /// or null.
        /// </summary>
        private static DateTimeOffset? ToUtc(DateTime? value)
        {
            if (value == null)
                return null;

            var dt = value.Value;
            if (dt.Kind == DateTimeKind.Unspecified)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            return new DateTimeOffset(dt.ToUniversalTime());
        }
    }
}
