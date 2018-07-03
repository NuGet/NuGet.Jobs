using System;
using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public class ValidationDurationIncidentParser : EnvironmentIncidentParser
    {
        private const string SubtitleRegEx = "Too many packages are stuck in the \"Validating\" state!";

        private readonly string _environment;

        public ValidationDurationIncidentParser(string environment)
            : base(SubtitleRegEx, environment)
        {
            _environment = environment;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = $"NuGet{SubComponent.ComponentPathDivider}Package Publishing";
            return true;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
