using NuGet.Services.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public abstract class DefaultIncidentParser : IncidentParser
    {
        private const string EnvironmentGroupName = "Environment";

        private readonly IEnumerable<string> _environments;
        private readonly int _maximumSeverity;

        public DefaultIncidentParser(string subtitleRegEx, IEnumerable<string> environments, int maximumSeverity)
            : base(GetRegEx(subtitleRegEx))
        {
            _environments = environments;
            _maximumSeverity = maximumSeverity;
        }

        protected override bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;

            if (incident.Severity > _maximumSeverity)
            {
                return false;
            }
            
            if (!_environments.Any(e => string.Equals(groups[EnvironmentGroupName].Value, e, StringComparison.OrdinalIgnoreCase)))
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
