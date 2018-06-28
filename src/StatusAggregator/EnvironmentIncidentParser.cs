using System;
using System.Text.RegularExpressions;

namespace StatusAggregator
{
    public class EnvironmentIncidentParser : IncidentParser
    {
        private const string EnvironmentGroupName = "Environment";

        private readonly string _environment;

        public EnvironmentIncidentParser(string subtitleRegEx, string environment)
            : base(GetRegEx(subtitleRegEx))
        {
            _environment = environment;
        }

        protected override bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;

            if (incident.Severity > 2)
            {
                return false;
            }

            if (!string.Equals(groups[EnvironmentGroupName].Value, _environment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            parsedIncident = new ParsedIncident(incident, $"NuGet{Component.ComponentPathDivider}Package Publishing", ComponentStatus.Degraded);
            return true;
        }

        private static string GetRegEx(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentGroupName}>.*)\] {subtitleRegEx}";
        }
    }
}
