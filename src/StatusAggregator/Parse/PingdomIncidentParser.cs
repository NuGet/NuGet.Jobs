using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public class PingdomIncidentParser : IncidentParser
    {
        private const string CheckNameGroupName = "CheckName";
        private const string CheckUrlGroupName = "CheckUrl";
        private static string SubtitleRegEx = $@"Pingdom check '(?<{CheckNameGroupName}>.*)' is failing! '(?<{CheckUrlGroupName}>.*)' is DOWN!";

        public PingdomIncidentParser(IEnumerable<IIncidentParsingFilter> filters)
            : base(SubtitleRegEx, filters)
        {
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var checkName = groups[CheckNameGroupName].Value;

            switch (checkName)
            {
                case "CDN DNS":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.RestoreName, Components.V3ProtocolName);
                    break;
                case "CDN Global":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.RestoreName, Components.V3ProtocolName, Components.GlobalRegionName);
                    break;
                case "CDN China":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.RestoreName, Components.V3ProtocolName, Components.ChinaRegionName);
                    break;
                case "Gallery DNS":
                case "Gallery Home":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.GalleryName);
                    break;
                case "Gallery USNC /":
                case "Gallery USNC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.GalleryName, Components.UsncInstanceName);
                    break;
                case "Gallery USSC /":
                case "Gallery USSC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.GalleryName, Components.UsscInstanceName);
                    break;
                case "Gallery USNC /api/v2/Packages()":
                case "Gallery USNC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.RestoreName, Components.V2ProtocolName, Components.UsncInstanceName);
                    break;
                case "Gallery USSC /api/v2/Packages()":
                case "Gallery USSC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.RestoreName, Components.V2ProtocolName, Components.UsscInstanceName);
                    break;
                case "Search USNC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.SearchName, Components.GlobalRegionName, Components.UsncInstanceName);
                    break;
                case "Search USSC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.SearchName, Components.GlobalRegionName, Components.UsscInstanceName);
                    break;
                case "Search EA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.SearchName, Components.ChinaRegionName, Components.EaInstanceName);
                    break;
                case "Search SEA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        Components.RootName, Components.SearchName, Components.ChinaRegionName, Components.SeaInstanceName);
                    break;
                default:
                    return false;
            }

            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
