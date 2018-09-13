// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class EventFactoryTests
    {
        public class TheCreateMethod : EventFactoryTest
        {
            public static IEnumerable<object[]> CreatesEvent_Data
            {
                get
                {
                    // Gracefully handles empty path.
                    yield return new object[] { "", "" };

                    // Handles path with single part.
                    var pathWithSinglePart = "hello";
                    yield return new object[] { pathWithSinglePart, pathWithSinglePart };

                    // Handles path with two parts.
                    var pathWithTwoParts = "hello" + Constants.ComponentPathDivider + "there";
                    yield return new object[] { pathWithTwoParts, pathWithTwoParts };

                    // Handles path with three parts and returns the first two.
                    var firstPartInThreePartPath = "hello";
                    var secondPartInThreePartPath = "there";
                    var thirdPartInThreePartPath = "friend";

                    var firstTwoPartsOfPathWithThreeParts =
                        firstPartInThreePartPath + Constants.ComponentPathDivider + secondPartInThreePartPath;
                    var pathWithThreeParts =
                        firstTwoPartsOfPathWithThreeParts + Constants.ComponentPathDivider + thirdPartInThreePartPath;

                    yield return new object[] { pathWithThreeParts, firstTwoPartsOfPathWithThreeParts };
                }
            }

            [Theory]
            [MemberData(nameof(CreatesEvent_Data))]
            public async Task CreatesEvent(string initialPath, string expectedPath)
            {
                var input = new ParsedIncident(Incident, initialPath, ComponentStatus.Up);

                EventEntity entity = null;
                Table
                    .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableEntity>(e =>
                    {
                        Assert.IsType<EventEntity>(e);
                        entity = e as EventEntity;
                    });

                var result = await Factory.Create(input);

                Assert.Equal(entity, result);
                Assert.Equal(expectedPath, entity.AffectedComponentPath);
                Assert.Equal(input.StartTime, entity.StartTime);

                Table
                    .Verify(
                        x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Once());
            }
        }

        public class EventFactoryTest
        {
            public Incident Incident = new Incident() {
                Source = new IncidentSourceData() {
                    CreateDate = new DateTime(2018, 9, 13) } };

            public Mock<ITableWrapper> Table { get; }
            public EventFactory Factory { get; }

            public EventFactoryTest()
            {
                Table = new Mock<ITableWrapper>();

                Factory = new EventFactory(
                    Table.Object,
                    Mock.Of<ILogger<EventFactory>>());
            }
        }
    }
}
