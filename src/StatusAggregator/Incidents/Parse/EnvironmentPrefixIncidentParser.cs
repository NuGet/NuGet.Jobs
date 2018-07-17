using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator.Incidents.Parse
{
    public abstract class EnvironmentPrefixIncidentParser : IncidentParser
    {
        public EnvironmentPrefixIncidentParser(string subtitleRegEx, IEnumerable<string> environments, IEnumerable<IIncidentParsingFilter> filters)
            : base(GetRegEx(subtitleRegEx), filters.Concat(new[] { new EnvironmentFilter(environments) }))
        {
        }

        private static string GetRegEx(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentFilter.EnvironmentGroupName}>.*)\] {subtitleRegEx}";
        }
    }
}
