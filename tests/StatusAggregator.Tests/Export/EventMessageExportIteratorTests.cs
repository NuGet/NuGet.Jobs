// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class EventMessageExportIteratorTests
    {
        public class TheProcessMethod : EventMessageExportIteratorTest
        {
            public EventEntity EventEntity = new EventEntity("", DefaultStartTime);

            [Fact]
            public void HandlesStartMessage()
            {
                var initialContext = new CurrentMessageExportContext(EventEntity);
                var message = new MessageEntity(EventEntity, DefaultStartTime, "hi", MessageType.Start);

                var committedContext = new CurrentMessageExportContext(EventEntity, new Event[0]);
                Handler
                    .Setup(x => x.CommitMessages(initialContext, true))
                    .Returns(committedContext);

                var addedContext = new CurrentMessageExportContext(EventEntity, new Event[0], new[] { message });
                Handler
                    .Setup(x => x.AddMessage(committedContext, message))
                    .Returns(addedContext);

                var result = Iterator.Process(initialContext, message);

                Assert.Equal(addedContext, result);
            }

            [Fact]
            public void HandlesEndMessage()
            {
                var initialContext = new CurrentMessageExportContext(EventEntity);
                var message = new MessageEntity(EventEntity, DefaultStartTime, "hi", MessageType.End);

                var addedContext = new CurrentMessageExportContext(EventEntity, new Event[0], new[] { message });
                Handler
                    .Setup(x => x.AddMessage(initialContext, message))
                    .Returns(addedContext);

                var committedContext = new CurrentMessageExportContext(
                    EventEntity, 
                    new[] { new Event("", DefaultStartTime, null, new[] { new Message(DefaultStartTime, "hi") }) });
                Handler
                    .Setup(x => x.CommitMessages(addedContext, true))
                    .Returns(committedContext);

                var result = Iterator.Process(initialContext, message);

                Assert.Equal(committedContext, result);
            }

            [Fact]
            public void HandlesManualMessage()
            {
                var initialContext = new CurrentMessageExportContext(EventEntity);
                var message = new MessageEntity(EventEntity, DefaultStartTime, "hi", MessageType.Manual);

                var addedContext = new CurrentMessageExportContext(EventEntity, new Event[0], new[] { message });
                Handler
                    .Setup(x => x.AddMessage(initialContext, message))
                    .Returns(addedContext);

                var result = Iterator.Process(initialContext, message);

                Assert.Equal(addedContext, result);
            }

            [Fact]
            public void ThrowsOnUnknownMessage()
            {
                var initialContext = new CurrentMessageExportContext(EventEntity);
                var message = new MessageEntity(EventEntity, DefaultStartTime, "hi", (MessageType)(-1));

                Assert.Throws<ArgumentException>(() => Iterator.Process(initialContext, message));
            }
        }

        public class TheExportMethod : EventMessageExportIteratorTest
        {
            public static IEnumerable<object[]> CommitsAndReturnsEvents_Data
            {
                get
                {
                    yield return new object[] { new EventEntity("", DefaultStartTime) };
                    yield return new object[] { new EventEntity("", DefaultStartTime, endTime: DefaultStartTime) };
                }
            }

            [Theory]
            [MemberData(nameof(CommitsAndReturnsEvents_Data))]
            public void CommitsAndReturnsEvents(EventEntity eventEntity)
            {
                var context = new CurrentMessageExportContext(eventEntity);

                var finalContext = new CurrentMessageExportContext(eventEntity, 
                    new[] { new Event("", DefaultStartTime, eventEntity.EndTime, new[] { new Message(DefaultStartTime, "hi") }) });
                Handler
                    .Setup(x => x.CommitMessages(context, eventEntity.IsActive))
                    .Returns(finalContext);

                var result = Iterator.Export(context);

                Assert.Equal(finalContext.Events, result);

                Handler
                    .Verify(
                        x => x.AddMessage(It.IsAny<CurrentMessageExportContext>(), It.IsAny<MessageEntity>()),
                        Times.Never());
            }
        }

        public class EventMessageExportIteratorTest
        {
            public static DateTime DefaultStartTime = new DateTime(2018, 9, 12);
            public Mock<IEventMessageExportIterationHandler> Handler { get; }

            public EventMessageExportIterator Iterator { get; }

            public EventMessageExportIteratorTest()
            {
                Handler = new Mock<IEventMessageExportIterationHandler>();

                Iterator = new EventMessageExportIterator(
                    Handler.Object,
                    Mock.Of<ILogger<EventMessageExportIterator>>());
            }
        }
    }
}
