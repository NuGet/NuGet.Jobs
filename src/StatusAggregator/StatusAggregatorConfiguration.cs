using NuGet.Jobs;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Parse;
using System;
using System.Collections.Generic;

namespace StatusAggregator
{
    public class StatusAggregatorConfiguration
    {
        /// <summary>
        /// A connection string for the storage account to use.
        /// </summary>
        public string StorageAccount { get; set; }

        /// <summary>
        /// The container name to export the <see cref="ServiceStatus"/> to.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The table name to persist the <see cref="ServiceStatus"/> in.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// A list of environments to filter incidents by.
        /// See <see cref="EnvironmentFilter"/>.
        /// </summary>
        public IEnumerable<string> Environments { get; set; }

        /// <summary>
        /// The maximum severity of any incidents to process.
        /// See <see cref="SeverityFilter"/>.
        /// </summary>
        public int MaximumSeverity { get; set; }

        /// <summary>
        /// A team ID to use to query the incident API.
        /// </summary>
        public string TeamId { get; set; }
    }
}
