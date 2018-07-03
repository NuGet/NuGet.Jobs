using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IIncidentUpdater
    {
        Task RefreshExistingIncidents();
        Task<DateTime?> FetchNewIncidents(DateTime cursor);
        Task UpdateActiveEvents(DateTime cursor);
    }
}
