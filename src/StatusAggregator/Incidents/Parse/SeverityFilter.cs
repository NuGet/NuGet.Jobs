using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public class SeverityFilter : IIncidentParsingFilter
    {
        private readonly int _maximumSeverity;

        public SeverityFilter(int maximumSeverity)
        {
            _maximumSeverity = maximumSeverity;
        }

        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            return incident.Severity <= _maximumSeverity;
        }
    }
}
