using NuGet.Services.Status;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public class OutdatedSearchServiceInstanceIncidentParser : DefaultIncidentParser
    {
        private const string SubtitleRegEx = "A search service instance is using an outdated index!";

        public OutdatedSearchServiceInstanceIncidentParser(IEnumerable<string> environments, int maximumSeverity)
            : base(SubtitleRegEx, environments, maximumSeverity)
        {
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = ComponentUtility.GetPath("NuGet", "Package Publishing");
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
