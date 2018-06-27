using System;
using System.Text.RegularExpressions;

namespace StatusAggregator
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
    }
}
