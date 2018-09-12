// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class EventExporterTests
    {
        public class TheExportMethod : EventExporterTest
        {
            [Fact]
            public void ExportsRecentEvents()
            {
                var oldEventEntity = new EventEntity("", 
                    new DateTime(2017, 9, 12), 
                    endTime: new DateTime(2017, 9, 13));

                var activeEvent1Entity = new EventEntity("", 
                    new DateTime(2017, 9, 12));
                var activeEvent2Entity = new EventEntity("", 
                    Cursor);

                var recentEvent1Entity = new EventEntity("", 
                    Cursor - EventVisibilityPeriod, 
                    endTime: Cursor - EventVisibilityPeriod);
                var recentEvent2Entity = new EventEntity("", 
                    Cursor - EventVisibilityPeriod, 
                    endTime: Cursor);

                var eventEntities = new[] 
                {
                    oldEventEntity,
                    activeEvent1Entity,
                    activeEvent2Entity,
                    recentEvent1Entity,
                    recentEvent2Entity
                };

                Table
                    .Setup(x => x.CreateQuery<EventEntity>())
                    .Returns(eventEntities.AsQueryable());

                var event1ForActiveEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, new[] { new Message(DateTime.MinValue, "") });
                var event2ForActiveEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, Enumerable.Empty<Message>());
                MessageExporter
                    .Setup(x => x.Export(activeEvent1Entity))
                    .Returns(new[] { event1ForActiveEvent1, event2ForActiveEvent1 })
                    .Verifiable();

                MessageExporter
                    .Setup(x => x.Export(activeEvent2Entity))
                    .Returns(new Event[0])
                    .Verifiable();

                var event1ForRecentEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, new[] { new Message(DateTime.MinValue, "") });
                var event2ForRecentEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, Enumerable.Empty<Message>());
                MessageExporter
                    .Setup(x => x.Export(recentEvent1Entity))
                    .Returns(new[] { event1ForRecentEvent1, event2ForRecentEvent1 })
                    .Verifiable();

                MessageExporter
                    .Setup(x => x.Export(recentEvent2Entity))
                    .Returns(new Event[0])
                    .Verifiable();

                var result = Exporter.Export(Cursor);

                var expectedEvents = new[] { event1ForActiveEvent1, event1ForRecentEvent1 };
                Assert.Equal(expectedEvents.Count(), result.Count());
                foreach (var expectedEvent in expectedEvents)
                {
                    Assert.Contains(expectedEvent, result);
                }

                MessageExporter.Verify();
                MessageExporter
                    .Verify(
                        x => x.Export(oldEventEntity),
                        Times.Never());
            }
        }

        public class EventExporterTest
        {
            public DateTime Cursor => new DateTime(2018, 9, 12);
            public TimeSpan EventVisibilityPeriod => TimeSpan.FromDays(10);
            public Mock<ITableWrapper> Table { get; }
            public Mock<IEventMessageExporter> MessageExporter { get; }
            public EventExporter Exporter { get; }

            public EventExporterTest()
            {
                Table = new Mock<ITableWrapper>();

                MessageExporter = new Mock<IEventMessageExporter>();

                var config = new StatusAggregatorConfiguration()
                {
                    EventVisibilityPeriodDays = EventVisibilityPeriod.Days
                };

                Exporter = new EventExporter(
                    Table.Object,
                    MessageExporter.Object,
                    config,
                    Mock.Of<ILogger<EventExporter>>());
            }
        }
    }
}
