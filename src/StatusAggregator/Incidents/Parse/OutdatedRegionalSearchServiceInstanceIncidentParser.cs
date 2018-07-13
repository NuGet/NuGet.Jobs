using System;
using System.Text.RegularExpressions;
using NuGet.Services.Status;

namespace StatusAggregator.Incidents.Parse
{
    public class OutdatedRegionalSearchServiceInstanceIncidentParser : EnvironmentIncidentParser
    {
        private const string ServiceEnvironmentGroupName = "SearchServiceName";
        private const string ServiceRegionGroupName = "SearchServiceName";
        private static string SubtitleRegEx = $@"Search service 'nuget-\[(?<{ServiceEnvironmentGroupName}>.*)\]-\[(?<{ServiceRegionGroupName}>.*)\]-search' is using an outdated index!";

        private readonly string _environment;

        public OutdatedRegionalSearchServiceInstanceIncidentParser(string environment)
            : base(SubtitleRegEx, environment)
        {
            _environment = environment;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var searchEnvironment = groups[ServiceEnvironmentGroupName].Value;
            var searchRegion = groups[ServiceRegionGroupName].Value;

            if (!string.Equals(searchEnvironment, _environment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string region;
            string subRegion;

            switch (searchRegion)
            {
                case "0":
                case "usnc":
                case "ussc":
                    region = "Global";
                    break;
                case "eastasia":
                case "southeastasia":
                    region = "Asia";
                    break;
                default:
                    return false;
            }

            switch (searchRegion)
            {
                case "0":
                case "usnc":
                    subRegion = "USNC";
                    break;
                case "ussc":
                    subRegion = "USSC";
                    break;
                case "eastasia":
                    subRegion = "EA";
                    break;
                case "southeastasia":
                    subRegion = "SEA";
                    break;
                default:
                    return false;
            }

            affectedComponentPath = ComponentUtility.GetPath("NuGet", "Search", region, subRegion);
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
