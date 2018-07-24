using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public class OutdatedSearchServiceInstanceIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string SubtitleRegEx = "A search service instance is using an outdated index!";

        public OutdatedSearchServiceInstanceIncidentParser(IEnumerable<string> environments, IEnumerable<IIncidentParsingFilter> filters)
            : base(SubtitleRegEx, environments, filters)
        {
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
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
