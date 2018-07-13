using NuGet.Services.Status;
using System;
using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public abstract class EnvironmentIncidentParser : IncidentParser
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

            if (!TryParseAffectedComponentPath(incident, groups, out var affectedComponentPath))
            {
                return false;
            }

            if (!TryParseAffectedComponentStatus(incident, groups, out var affectedComponentStatus))
            {
                return false;
            }

            parsedIncident = new ParsedIncident(incident, affectedComponentPath, affectedComponentStatus);
            return true;
        }

        protected abstract bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath);

        protected abstract bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus);

        private static string GetRegEx(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentGroupName}>.*)\] {subtitleRegEx}";
        }
    }
}
