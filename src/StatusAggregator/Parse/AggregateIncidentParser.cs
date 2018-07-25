using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using System.Collections.Generic;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Default implementation of <see cref="IAggregateIncidentParser"/> that returns all <see cref="ParsedIncident"/>s returned by its <see cref="IIncidentParser"/>s.
    /// </summary>
    public class AggregateIncidentParser : IAggregateIncidentParser
    {
        private readonly IEnumerable<IIncidentParser> _incidentParsers;

        private readonly ILogger<AggregateIncidentParser> _logger;

        public AggregateIncidentParser(
            IEnumerable<IIncidentParser> incidentParsers,
            ILogger<AggregateIncidentParser> logger)
        {
            _incidentParsers = incidentParsers;
            _logger = logger;
        }

        public IEnumerable<ParsedIncident> ParseIncident(Incident incident)
        {
            using (_logger.Scope(
                "Beginning to parse incident.",
                "Finished parsing incident.",
                "Parsing incident {IncidentId}", incident.Id))
            {
                var parsedIncidents = new List<ParsedIncident>();
                foreach (var incidentParser in _incidentParsers)
                {
                    if (incidentParser.TryParseIncident(incident, out var parsedIncident))
                    {
                        parsedIncidents.Add(parsedIncident);
                    }
                }

                return parsedIncidents;
            }
        }
    }
}
