using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatusAggregator.Incidents
{
    public interface IIncidentCollector
    {
        Task<Incident> GetIncident(string incidentApiId);

        Task<IEnumerable<Incident>> GetRecentIncidents(DateTime since);
    }
}
