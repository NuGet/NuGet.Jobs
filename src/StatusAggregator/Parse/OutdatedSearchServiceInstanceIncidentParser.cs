using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public class OutdatedSearchServiceInstanceIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string SubtitleRegEx = "A search service instance is using an outdated index!";

        public OutdatedSearchServiceInstanceIncidentParser(
            IEnumerable<IIncidentParsingFilter> filters, 
            ILogger<OutdatedSearchServiceInstanceIncidentParser> logger)
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
