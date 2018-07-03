using System;
using System.Collections.Generic;

namespace StatusAggregator.Incidents.Parse
{
    public class AggregateIncidentParser : IAggregateIncidentParser
    {
        private readonly IEnumerable<IIncidentParser> _incidentParsers;

        public AggregateIncidentParser(IEnumerable<IIncidentParser> incidentParsers)
        {
            _incidentParsers = incidentParsers;
        }

        public IEnumerable<ParsedIncident> ParseIncident(Incident incident)
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
