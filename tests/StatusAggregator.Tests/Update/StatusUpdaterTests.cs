// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using StatusAggregator.Collector;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class StatusUpdaterTests
    {
        public class StatusUpdaterTest
        {
            public Mock<ICursor> Cursor { get; }
            public Mock<IEnumerable<IEntityCollector>> Collectors { get; }
        }

        public class TheConstructor
        {
            [Fact]
            public void ThrowsWithoutCursor()
            {
                var incidentCollector = new Mock<IEntityCollector>();
                incidentCollector
                    .Setup(x => x.Name)
                    .Returns(IncidentEntityCollectorProcessor.IncidentsCollectorName);

                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        null,
                        new[] { incidentCollector.Object },
                        Mock.Of<IActiveEventEntityUpdater>(),
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            public static IEnumerable<object[]> ThrowsWithoutCollectors_Data
            {
                get
                {
                    // null enumerable
                    yield return new object[] { typeof(ArgumentNullException), null };
                    // empty enumerable
                    yield return new object[] { typeof(ArgumentException), new IEntityCollector[0] };
                    // enumerable without incident collector
                    yield return new object[] { typeof(ArgumentException), };
                }
            }

            [Theory]
            [MemberData(nameof(ThrowsWithoutCollectors_Data))]
            public void ThrowsWithoutCollectors(Type exceptionType, IEnumerable<IEntityCollector> collectors)
            {
                Assert.Throws(
                    exceptionType,
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        collectors,
                        Mock.Of<IActiveEventEntityUpdater>(),
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            [Fact]
            public void ThrowsWithoutActiveEventUpdater()
            {
                var incidentCollector = new Mock<IEntityCollector>();
                incidentCollector
                    .Setup(x => x.Name)
                    .Returns(IncidentEntityCollectorProcessor.IncidentsCollectorName);

                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        new[] { incidentCollector.Object },
                        null,
                        Mock.Of<ILogger<StatusUpdater>>()));
            }

            [Fact]
            public void ThrowsWithoutLogger()
            {
                var incidentCollector = new Mock<IEntityCollector>();
                incidentCollector
                    .Setup(x => x.Name)
                    .Returns(IncidentEntityCollectorProcessor.IncidentsCollectorName);

                Assert.Throws<ArgumentNullException>(
                    () => new StatusUpdater(
                        Mock.Of<ICursor>(),
                        new[] { incidentCollector.Object },
                        Mock.Of<IActiveEventEntityUpdater>(),
                        null));
            }
        }
    }
}
