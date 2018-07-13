using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Services.Status;

namespace StatusAggregator.Incidents.Parse
{
    public class ValidationDurationIncidentParser : DefaultIncidentParser
    {
        private const string SubtitleRegEx = "Too many packages are stuck in the \"Validating\" state!";

        public ValidationDurationIncidentParser(IEnumerable<string> environments, int maximumSeverity)
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
