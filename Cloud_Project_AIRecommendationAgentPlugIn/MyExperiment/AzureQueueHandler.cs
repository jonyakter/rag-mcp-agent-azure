using MyCloudProject.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyExperiment
{
    /// <summary>
    /// Handles the "queue" trigger for the experiment.
    /// Real Azure Queue polling is replaced by a single read of the
    /// EXPERIMENT_INPUT environment variable — Program.cs's while-loop
    /// stays unchanged: first call delivers the message, every call after
    /// that returns null so the loop idles normally.
    /// </summary>
    public class AzureQueueHandler
    {
        private bool _alreadyDelivered = false;

        public AzureQueueHandler(string connectionString, string queueName)
        {
        }

        /// <summary>
        /// Returns one message built from EXPERIMENT_INPUT on the first call,
        /// then null on every subsequent call.
        /// </summary>
        public IExerimentRequest? Receive(CancellationToken token)
        {
            if (_alreadyDelivered)
                return null;

            string? input = Environment.GetEnvironmentVariable("EXPERIMENT_INPUT");

            if (string.IsNullOrWhiteSpace(input))
                return null;

            _alreadyDelivered = true;

            return new ExerimentRequestMessage
            {
                ExperimentId = Guid.NewGuid().ToString(),
                InputFile = input,
                Name = "AI Recommendation Experiment",
                Description = "Triggered via EXPERIMENT_INPUT: " + input,
                MessageId = string.Empty,
                MessageReceipt = string.Empty
            };
        }

        public Task CommitAsync(IExerimentRequest request)
        {
            return Task.CompletedTask;
        }
    }
}
