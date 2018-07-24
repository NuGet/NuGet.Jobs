// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public abstract class IncidentParser : IIncidentParser
    {
        private readonly string _regExPattern;

        private readonly IEnumerable<IIncidentParsingFilter> _filters;

        public IncidentParser(string regExPattern)
        {
            _regExPattern = regExPattern;
            _filters = Enumerable.Empty<IIncidentParsingFilter>();
        }

        public IncidentParser(string regExPattern, IEnumerable<IIncidentParsingFilter> filters)
            : this(regExPattern)
        {
            _filters = filters.ToList();
        }

        public bool TryParseIncident(Incident incident, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;
            var match = Regex.Match(incident.Title, _regExPattern);
            return match.Success && TryParseIncident(incident, match.Groups, out parsedIncident);
        }

        protected bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;
            
            if (_filters.Any(f => !f.ShouldParse(incident, groups)))
            {
                return false;
            }

            if (!TryParseAffectedComponentPath(incident, groups, out var affectedComponentPath))
            {
                return false;
            }

            if (!TryParseAffectedComponentStatus(incident, groups, out var affectedComponentStatus))
            {
                return false;
            }

            parsedIncident = new ParsedIncident(incident, affectedComponentPath, affectedComponentStatus);
            return true;
        }

        protected abstract bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath);

        protected abstract bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus);
    }
}
