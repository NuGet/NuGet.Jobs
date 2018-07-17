using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public class EnvironmentFilter : IIncidentParsingFilter
    {
        public const string EnvironmentGroupName = "Environment";

        public IEnumerable<string> Environments { get; }

        public EnvironmentFilter(IEnumerable<string> environments)
        {
            Environments = environments;
        }

        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            return Environments.Any(
                e => string.Equals(groups[EnvironmentGroupName].Value, e, StringComparison.OrdinalIgnoreCase));
        }
    }
}
