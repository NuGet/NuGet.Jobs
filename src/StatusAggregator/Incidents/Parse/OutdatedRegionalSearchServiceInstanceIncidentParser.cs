using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Status;

namespace StatusAggregator.Incidents.Parse
{
    public class OutdatedRegionalSearchServiceInstanceIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string ServiceEnvironmentGroupName = "SearchEnvironment";
        private const string ServiceRegionGroupName = "SearchRegion";
        private static string SubtitleRegEx = $@"Search service 'nuget-(?<{ServiceEnvironmentGroupName}>.*)-(?<{ServiceRegionGroupName}>.*)-(v2v3)?search' is using an outdated index!";

        private readonly IEnumerable<string> _environments;

        public OutdatedRegionalSearchServiceInstanceIncidentParser(IEnumerable<string> environments, IEnumerable<IIncidentParsingFilter> filters)
            : base(SubtitleRegEx, environments, filters)
        {
            _environments = environments;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var searchEnvironment = groups[ServiceEnvironmentGroupName].Value;
            var searchRegion = groups[ServiceRegionGroupName].Value;

            if (!_environments.Any(e => string.Equals(searchEnvironment, e, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            affectedComponentPath = ComponentUtility.GetPath(Components.RootName, Components.UploadName);
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
