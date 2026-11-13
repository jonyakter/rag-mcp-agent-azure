
using System;
using System.Collections.Generic;
using System.Text;

namespace MyCloudProject.Common
{
    /// <summary>
    /// Defines the contract for the result produced by running an experiment.
    /// </summary>
    public interface IExperimentResult
    {
        /// <summary>
        /// Any identifier of your choice.
        /// </summary>
        string ExperimentId { get; set; }

        /// <summary>
        /// The URI of the uploaded output file for this experiment's result.
        /// </summary>
        public string InputFileUrl { get; set; }

        /// <summary>
        /// The UTC time the experiment started.
        /// </summary>
        DateTime? StartTimeUtc { get; set; }

        /// <summary>
        /// The UTC time the experiment finished.
        /// </summary>
        DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// The total time the experiment took to run.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

}
