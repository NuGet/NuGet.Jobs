using System.Collections.Generic;

namespace StatusAggregator.Incidents.Parse
{
    public interface IAggregateIncidentParser
    {
        IEnumerable<ParsedIncident> ParseIncident(Incident incident);
    }
}
