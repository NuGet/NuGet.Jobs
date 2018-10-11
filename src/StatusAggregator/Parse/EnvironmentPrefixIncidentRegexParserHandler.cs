// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Incidents;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Subclass of <see cref="IncidentRegexParsingHandler"/> that expects <see cref="Incident"/>s are prefixed with "[ENVIRONMENT]".
    /// </summary>
    public abstract class EnvironmentPrefixIncidentRegexParserHandler : IncidentRegexParsingHandler
    {
        public EnvironmentPrefixIncidentRegexParserHandler(
            string subtitleRegEx,
            IEnumerable<IIncidentRegexParsingFilter> filters)
            : base(
                  PrependEnvironmentRegexGroup(subtitleRegEx),
                  filters)
        {
            if (!filters.Any(f => f is EnvironmentRegexFilter))
            {
                throw new ArgumentException(
                    $"A {nameof(EnvironmentPrefixIncidentRegexParserHandler)} must be run with an {nameof(EnvironmentRegexFilter)}!", 
                    nameof(filters));
            }
        }

        private static string PrependEnvironmentRegexGroup(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentRegexFilter.EnvironmentGroupName}>.*)\] {subtitleRegEx}";
        }
    }
}
