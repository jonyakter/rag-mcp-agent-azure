using Azure;
using Azure.Data.Tables;
using MyCloudProject.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyExperiment
{

    /// <summary>
    /// Azure Table Storage entity holding the outcome of a single experiment run.
    /// </summary>
    public class ExperimentResult : ITableEntity, IExperimentResult
    {
        public ExperimentResult(string partitionKey, string rowKey)
        {
            this.PartitionKey = partitionKey;
            this.RowKey = rowKey;
        }

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public required string ExperimentId { get; set; }

        public required string Name { get; set; }

        public required string Description { get; set; }

        public DateTime? StartTimeUtc { get; set; }

        public DateTime? EndTimeUtc { get; set; }

        public long DurationSec { get; set; }

        public required string InputFileUrl { get; set; }

        public required string[] OutputFiles { get; set; }

        public float Accuracy { get; set; }

        public TimeSpan Duration { get; set; }
    }
}
