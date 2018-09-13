// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class EventMessageExportIterationHandlerTests
    {
        public class TheAddMessageMethod : EventMessageExportIterationHandlerTest
        {
            [Fact]
            public void AddsMessage()
            {
                var initialEvents = new[] { new Event("", DefaultStartTime, DefaultStartTime, new Message[0]) };
                var initialMessages = new[] { new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(2), "hi", MessageType.Manual) };
                var initialContext = new CurrentMessageExportContext(EventEntity, initialEvents, initialMessages);
                var newMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(1), "hello", MessageType.Manual);

                var result = Handler.AddMessage(initialContext, newMessage);

                Assert.Equal(EventEntity, result.EventEntity);
                Assert.Equal(initialEvents, result.Events);

                Assert.Equal(
                    initialContext.UncommittedMessages.Count() + 1, 
                    result.UncommittedMessages.Count());
                
                for (var i = 0; i < initialMessages.Count(); i++)
                {
                    Assert.Equal(initialMessages[i], result.UncommittedMessages.ElementAt(i));
                }

                Assert.Equal(newMessage, result.UncommittedMessages.Last());
            }
        }

        public class TheCommitMessagesMethod : EventMessageExportIterationHandlerTest
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ReturnsInitialContextIfNoUncommittedMessages(bool isActive)
            {
                var initialEvents = new[] { new Event("", DefaultStartTime, DefaultStartTime, new Message[0]) };
                var initialContext = new CurrentMessageExportContext(EventEntity, initialEvents);

                var result = Handler.CommitMessages(initialContext, isActive);

                Assert.Equal(initialContext, result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ReturnsCommittedContextWithUncommittedMessages(bool isActive)
            {
                var initialEvent = new Event("", DefaultStartTime, DefaultStartTime, new Message[0]);
                var initialEvents = new[] { initialEvent };

                var firstMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(1), "one", MessageType.Manual);
                var secondMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(2), "two", MessageType.Manual);
                var thirdMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(3), "three", MessageType.Manual);
                var fourthMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(4), "four", MessageType.Manual);
                var initialMessages = new[] { secondMessage, firstMessage, fourthMessage, thirdMessage };

                var initialContext = new CurrentMessageExportContext(EventEntity, initialEvents, initialMessages);

                var result = Handler.CommitMessages(initialContext, isActive);

                Assert.Equal(EventEntity, result.EventEntity);

                Assert.Empty(result.UncommittedMessages);

                Assert.Equal(2, result.Events.Count());
                Assert.Equal(initialEvent, result.Events.First());

                var actualEvent = result.Events.Last();
                Assert.Equal(firstMessage.Time, actualEvent.StartTime);

                if (isActive)
                {
                    Assert.Null(actualEvent.EndTime);
                }
                else
                {
                    Assert.Equal(fourthMessage.Time, actualEvent.EndTime);
                }

                AssertMessageAt(0, firstMessage, actualEvent.Messages);
                AssertMessageAt(1, secondMessage, actualEvent.Messages);
                AssertMessageAt(2, thirdMessage, actualEvent.Messages);
                AssertMessageAt(3, fourthMessage, actualEvent.Messages);
            }
            
            private void AssertMessageAt(int index, MessageEntity message, IEnumerable<Message> messages)
            {
                AssertMessage(message, messages.ElementAt(index));
            }

            private void AssertMessage(MessageEntity expected, Message actual)
            {
                Assert.Equal(expected.Time, actual.Time);
                Assert.Equal(expected.Contents, actual.Contents);
            }
        }

        public class EventMessageExportIterationHandlerTest
        {
            public static DateTime DefaultStartTime = new DateTime(2018, 9, 13);
            public EventEntity EventEntity = new EventEntity("", DefaultStartTime);
            public EventMessageExportIterationHandler Handler { get; }

            public EventMessageExportIterationHandlerTest()
            {
                Handler = new EventMessageExportIterationHandler(
                    Mock.Of<ILogger<EventMessageExportIterationHandler>>());
            }
        }
    }
}
