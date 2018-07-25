using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    /// <summary>
    /// Handles updating any active <see cref="IncidentEntity"/>s.
    /// </summary>
    public interface IIncidentUpdater
    {
        /// <summary>
        /// Update the status of any active <see cref="IncidentEntity"/>s.
        /// </summary>
        Task RefreshExistingIncidents();

        /// <summary>
        /// Fetches any new <see cref="Incident"/>s and processes them.
        /// </summary>
        /// <param name="cursor">The current timestamp processed by the job.</param>
        /// <returns>The most recent <see cref="Incident.CreateDate"/> processed by the job or null if no <see cref="Incident"/>s were processed.</returns>
        Task<DateTime?> FetchNewIncidents(DateTime cursor);
    }
}
