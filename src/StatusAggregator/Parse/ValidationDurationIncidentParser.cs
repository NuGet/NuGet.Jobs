﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public class ValidationDurationIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string SubtitleRegEx = "Too many packages are stuck in the \"Validating\" state!";

        public ValidationDurationIncidentParser(
            IEnumerable<IIncidentParsingFilter> filters,
            ILogger<ValidationDurationIncidentParser> logger)
            : base(SubtitleRegEx, filters, logger)
        {
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.UploadName);
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
