// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Parse
{
    public class AIAvailabilityIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod : AIAvailabilityIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsFalseIfNoComponentPath()
            {
                var match = ParsingUtility.GetMatchWithGroups(
                    new KeyValuePair<string, string>(AIAvailabilityIncidentRegexParsingHandler.TestGroupName, "test"),
                    new KeyValuePair<string, string>(EnvironmentRegexParsingFilter.EnvironmentGroupName, Environment),
                    new KeyValuePair<string, string>(AIAvailabilityIncidentRegexParsingHandler.AffectedComponentPathGroupName, null));

                var result = Handler.TryParseAffectedComponentPath(Incident, match.Groups, out var affectedComponentPath);

                Assert.False(result);
            }
            [Fact]
            public void ReturnsTrueIfComponentPathSpecified()
            {
                var expectedPath = "hello/hi/howdy";

                var match = ParsingUtility.GetMatchWithGroups(
                    new KeyValuePair<string, string>(AIAvailabilityIncidentRegexParsingHandler.TestGroupName, "test"),
                    new KeyValuePair<string, string>(EnvironmentRegexParsingFilter.EnvironmentGroupName, Environment),
                    new KeyValuePair<string, string>(AIAvailabilityIncidentRegexParsingHandler.AffectedComponentPathGroupName, expectedPath));

                var result = Handler.TryParseAffectedComponentPath(Incident, match.Groups, out var actualPath);

                Assert.True(result);
                Assert.Equal(expectedPath, actualPath);
            }
        }

        public class AIAvailabilityIncidentRegexParsingHandlerTest
        {
            public string Environment = "env";
            public Incident Incident = new Incident();
            public AIAvailabilityIncidentRegexParsingHandler Handler { get; }

            public AIAvailabilityIncidentRegexParsingHandlerTest()
            {
                Handler = Construct(
                    new[] { ParsingUtility.CreateEnvironmentFilter(Environment) });
            }
        }

        public class TheConstructor
            : EnvironmentPrefixIncidentRegexParsingHandlerTests.TheConstructor<AIAvailabilityIncidentRegexParsingHandler>
        {
            protected override AIAvailabilityIncidentRegexParsingHandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters)
            {
                return Construct(filters.ToArray());
            }
        }

        public static AIAvailabilityIncidentRegexParsingHandler Construct(params IIncidentRegexParsingFilter[] filters)
        {
            return new AIAvailabilityIncidentRegexParsingHandler(
                filters,
                Mock.Of<ILogger<AIAvailabilityIncidentRegexParsingHandler>>());
        }
    }
}
