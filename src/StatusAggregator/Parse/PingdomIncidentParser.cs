// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public class PingdomIncidentParser : IncidentParser
    {
        private const string CheckNameGroupName = "CheckName";
        private const string CheckUrlGroupName = "CheckUrl";
        private static string SubtitleRegEx = $@"Pingdom check '(?<{CheckNameGroupName}>.*)' is failing! '(?<{CheckUrlGroupName}>.*)' is DOWN!";

        private readonly ILogger<PingdomIncidentParser> _logger;

        public PingdomIncidentParser(
            IEnumerable<IIncidentParsingFilter> filters,
            ILogger<PingdomIncidentParser> logger)
            : base(SubtitleRegEx, filters, logger)
        {
            _logger = logger;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var checkName = groups[CheckNameGroupName].Value;
            _logger.LogInformation("Check name is {CheckName}.", checkName);

            switch (checkName)
            {
                case "CDN DNS":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName);
                    break;
                case "CDN Global":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName, ComponentFactory.GlobalRegionName);
                    break;
                case "CDN China":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName, ComponentFactory.ChinaRegionName);
                    break;
                case "Gallery DNS":
                case "Gallery Home":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.GalleryName);
                    break;
                case "Gallery USNC /":
                case "Gallery USNC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.GalleryName, ComponentFactory.UsncInstanceName);
                    break;
                case "Gallery USSC /":
                case "Gallery USSC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.GalleryName, ComponentFactory.UsscInstanceName);
                    break;
                case "Gallery USNC /api/v2/Packages()":
                case "Gallery USNC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V2ProtocolName, ComponentFactory.UsncInstanceName);
                    break;
                case "Gallery USSC /api/v2/Packages()":
                case "Gallery USSC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V2ProtocolName, ComponentFactory.UsscInstanceName);
                    break;
                case "Search USNC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.GlobalRegionName, ComponentFactory.UsncInstanceName);
                    break;
                case "Search USSC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.GlobalRegionName, ComponentFactory.UsscInstanceName);
                    break;
                case "Search EA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.ChinaRegionName, ComponentFactory.EaInstanceName);
                    break;
                case "Search SEA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.ChinaRegionName, ComponentFactory.SeaInstanceName);
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
