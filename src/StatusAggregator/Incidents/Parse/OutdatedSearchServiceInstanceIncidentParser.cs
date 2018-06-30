using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public class OutdatedSearchServiceInstanceIncidentParser : EnvironmentIncidentParser
    {
        private const string SubtitleRegEx = "A search service instance is using an outdated index!";

        private readonly string _environment;

        public OutdatedSearchServiceInstanceIncidentParser(string environment)
            : base(SubtitleRegEx, environment)
        {
            _environment = environment;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = $"NuGet{Component.ComponentPathDivider}Package Publishing";
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
