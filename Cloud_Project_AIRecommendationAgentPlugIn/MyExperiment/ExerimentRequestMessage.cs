using MyCloudProject.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace MyExperiment
{
    /// <summary>
    /// Experiment request as deserialized from an incoming Azure Storage Queue message,
    /// carrying the queue's message ID/receipt needed to delete it after processing.
    /// </summary>
    internal class ExerimentRequestMessage : IExerimentRequest
    {
        public required string ExperimentId { get; set; }
        public required string InputFile { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>The queue message's unique ID.</summary>
        public required string MessageId { get; set; }

        /// <summary>The queue message's pop receipt, required to delete it after processing.</summary>
        public required string MessageReceipt { get; set; }
    }
}
