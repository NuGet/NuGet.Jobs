using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public class TrafficManagerEndpointStatusIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string DomainGroupName = "Domain";
        private const string TargetGroupName = "Target";
        private static string SubtitleRegEx = $"Traffic Manager for (?<{DomainGroupName}>.*) is reporting (?<{TargetGroupName}>.*) as not Online!";

        private readonly ILogger<TrafficManagerEndpointStatusIncidentParser> _logger;

        public TrafficManagerEndpointStatusIncidentParser(
            IEnumerable<IIncidentParsingFilter> filters,
            ILogger<TrafficManagerEndpointStatusIncidentParser> logger)
            : base(SubtitleRegEx, filters, logger)
        {
            _logger = logger;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var domain = groups[DomainGroupName].Value;
            _logger.LogInformation("Domain is {Domain}.", domain);

            var target = groups[TargetGroupName].Value;
            _logger.LogInformation("Target is {Target}.", target);

            switch (domain)
            {
                case "devnugettest.trafficmanager.net":
                    switch (target)
                    {
                        case "nuget-dev-use2-gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName, 
                                NuGetServiceComponentFactory.GalleryName, 
                                NuGetServiceComponentFactory.UsncInstanceName);
                            break;
                        case "nuget-dev-ussc-gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName,
                                NuGetServiceComponentFactory.GalleryName,
                                NuGetServiceComponentFactory.UsscInstanceName);
                            break;
                        default:
                            return false;
                    }
                    break;
                case "nuget-int-test-failover.trafficmanager.net":
                    switch (target)
                    {
                        case "nuget-int-0-v2gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName,
                                NuGetServiceComponentFactory.GalleryName,
                                NuGetServiceComponentFactory.UsncInstanceName);
                            break;
                        case "nuget-int-ussc-gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName,
                                NuGetServiceComponentFactory.GalleryName,
                                NuGetServiceComponentFactory.UsscInstanceName);
                            break;
                        default:
                            return false;
                    }
                    break;
                case "nuget-prod-v2gallery.trafficmanager.net":
                    switch (target)
                    {
                        case "nuget-prod-0-v2gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName,
                                NuGetServiceComponentFactory.GalleryName,
                                NuGetServiceComponentFactory.UsncInstanceName);
                            break;
                        case "nuget-prod-ussc-gallery.cloudapp.net":
                            affectedComponentPath = ComponentUtility.GetPath(
                                NuGetServiceComponentFactory.RootName,
                                NuGetServiceComponentFactory.GalleryName,
                                NuGetServiceComponentFactory.UsscInstanceName);
                            break;
                        default:
                            return false;
                    }
                    break;
                default:
                    return false;
            }

            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Down;
            return true;
        }
    }
}
