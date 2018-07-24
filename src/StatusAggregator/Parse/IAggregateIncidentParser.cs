using NuGet.Services.Incidents;
using System.Collections.Generic;

namespace StatusAggregator.Parse
{
    public interface IAggregateIncidentParser
    {
        IEnumerable<ParsedIncident> ParseIncident(Incident incident);
    }
}
