using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Services.Status;

namespace StatusAggregator.Incidents.Parse
{
    public class ValidationDurationIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string SubtitleRegEx = "Too many packages are stuck in the \"Validating\" state!";

        public ValidationDurationIncidentParser(IEnumerable<string> environments, IEnumerable<IIncidentParsingFilter> filters)
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
