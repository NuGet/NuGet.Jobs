// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;

namespace StatusAggregator
{
    public abstract class IncidentParser : IIncidentParser
    {
        private readonly string _regExPattern;

        public IncidentParser(string regExPattern)
        {
            _regExPattern = regExPattern;
        }

        public bool TryParseIncident(Incident incident, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;
            var match = Regex.Match(incident.Title, _regExPattern);
            return match.Success && TryParseIncident(incident, match.Groups, out parsedIncident);
        }

        protected abstract bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident);
    }
}
