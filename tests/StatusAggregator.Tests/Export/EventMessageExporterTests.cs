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
    public class EventMessageExporterTests
    {
        public class TheExportMethod : EventMessageExporterTest
        {
            [Fact]
            public void ExportsEventMessagesWithContent()
            {
                var differentEvent = new EventEntity("", DefaultStartTime + TimeSpan.FromDays(1));
                var differentEventMessage = new MessageEntity(differentEvent, DefaultStartTime, "", MessageType.Manual);

                var emptyMessage = new MessageEntity(EventEntity, DefaultStartTime, "", MessageType.Manual);
                var firstMessage = new MessageEntity(EventEntity, DefaultStartTime, "hi", MessageType.Manual);
                var secondMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(1), "hi", MessageType.Manual);

                var messages = new[] { differentEventMessage, secondMessage, firstMessage, emptyMessage };
                Table
                    .Setup(x => x.CreateQuery<MessageEntity>())
                    .Returns(messages.AsQueryable());

                Iterator
                    .Verify(
                        x => x.Process(null, It.IsAny<MessageEntity>()),
                        Times.Never());

                var firstIterationContext = new CurrentMessageExportContext(EventEntity);
                Iterator
                    .Setup(x => x.Process(
                        It.Is<CurrentMessageExportContext>(c => c.EventEntity == EventEntity && !c.Events.Any() && !c.UncommittedMessages.Any()),
                        firstMessage))
                    .Returns(firstIterationContext);

                var secondIterationContext = new CurrentMessageExportContext(EventEntity);
                Iterator
                    .Setup(x => x.Process(
                        firstIterationContext,
                        secondMessage))
                    .Returns(secondIterationContext);

                Iterator
                    .Verify(
                        x => x.Export(null),
                        Times.Never());

                var exportedEnumerable = new[] { new Event("", DefaultStartTime, null, new[] { new Message(DefaultStartTime, "hi") }) };
                Iterator
                    .Setup(x => x.Export(secondIterationContext))
                    .Returns(exportedEnumerable);

                var result = Exporter.Export(EventEntity);

                Assert.Equal(exportedEnumerable, result);

                Iterator
                    .Verify(
                        x => x.Process(
                            It.IsAny<CurrentMessageExportContext>(), 
                            differentEventMessage),
                        Times.Never());

                Iterator
                    .Verify(
                        x => x.Process(
                            It.IsAny<CurrentMessageExportContext>(),
                            emptyMessage),
                        Times.Never());

                Iterator
                    .Verify(
                        x => x.Process(
                            It.IsAny<CurrentMessageExportContext>(),
                            firstMessage),
                        Times.Once());

                Iterator
                    .Verify(
                        x => x.Process(
                            It.IsAny<CurrentMessageExportContext>(),
                            secondMessage),
                        Times.Once());
            }
        }

        public class EventMessageExporterTest
        {
            public DateTime DefaultStartTime = new DateTime(2018, 9, 12);
            public EventEntity EventEntity { get; }
            public Mock<ITableWrapper> Table { get; }
            public Mock<IEventMessageExportIterator> Iterator { get; }
            public EventMessageExporter Exporter { get; }

            public EventMessageExporterTest()
            {
                EventEntity = new EventEntity("", DefaultStartTime);

                Table = new Mock<ITableWrapper>();

                Iterator = new Mock<IEventMessageExportIterator>();

                Exporter = new EventMessageExporter(
                    Table.Object,
                    Iterator.Object,
                    Mock.Of<ILogger<EventMessageExporter>>());
            }
        }
    }
}
