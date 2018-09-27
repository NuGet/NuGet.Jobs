// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageChangeEventProcessorTests
    {
        public class TheProcessMethod : MessageChangeEventProcessorTest
        {
            public static IEnumerable<object[]> AllMessageTypes_Data
            {
                get
                {
                    yield return new object[] { MessageType.Start };
                    yield return new object[] { MessageType.End };
                    yield return new object[] { MessageType.Manual };
                }
            }

            public static IEnumerable<object[]> AllImpactedStatuses_Data
            {
                get
                {
                    yield return new object[] { ComponentStatus.Degraded };
                    yield return new object[] { ComponentStatus.Down };
                }
            }

            [Theory]
            [MemberData(nameof(AllMessageTypes_Data))]
            public async Task HandlesUnexpectedPathGracefully(MessageType type)
            {
                var change = new MessageChangeEvent(
                    DefaultTimestamp, 
                    "missingPath", 
                    ComponentStatus.Degraded,
                    type);

                var root = new TestComponent("hi");

                var context = new CurrentMessageContext(
                    DefaultTimestamp, 
                    new TestComponent("name"), 
                    ComponentStatus.Down);

                var result = await Processor.Process(change, EventEntity, root, context);

                Assert.Equal(context, result);

                Factory
                    .Verify(
                        x => x.CreateMessage(
                            It.IsAny<EventEntity>(), 
                            It.IsAny<DateTime>(), 
                            It.IsAny<MessageType>(), 
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Fact]
            public async Task IgnoresStartMessageWhereComponentDoesntAffectStatus()
            {
                var hiddenChild = new TestComponent("child");
                var root = new TestComponent("hi", new[] { hiddenChild }, false);

                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.GetByNames<IComponent>(root.Name, hiddenChild.Name).Path,
                    ComponentStatus.Degraded,
                    MessageType.Start);

                var context = new CurrentMessageContext(
                    DefaultTimestamp,
                    new TestComponent("name"),
                    ComponentStatus.Down);

                var result = await Processor.Process(change, EventEntity, root, context);

                Assert.Equal(context, result);

                Factory
                    .Verify(
                        x => x.CreateMessage(
                            It.IsAny<EventEntity>(),
                            It.IsAny<DateTime>(),
                            It.IsAny<MessageType>(),
                            It.IsAny<IComponent>()),
                        Times.Never());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatuses_Data))]
            public async Task CreatesStartMessageWithNullContextForHiddenComponent(ComponentStatus status)
            {
                var child = new TestComponent("child");
                var root = new PrimarySecondaryComponent("hi", "", new[] { child });

                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    root.GetByNames<IComponent>(root.Name, child.Name).Path,
                    status,
                    MessageType.Start);

                var result = await Processor.Process(change, EventEntity, root, null);

                Assert.Equal(change.Timestamp, result.Timestamp);
                Assert.Equal(root, result.AffectedComponent);
                Assert.Equal(change.AffectedComponentStatus, result.AffectedComponentStatus);

                Factory
                    .Verify(
                        x => x.CreateMessage(
                            EventEntity,
                            DefaultTimestamp,
                            MessageType.Start,
                            root),
                        Times.Once());
            }

            [Theory]
            [MemberData(nameof(AllImpactedStatuses_Data))]
            public async Task CreatesStartMessageWithNullContext(ComponentStatus status)
            {
                var child = new TestComponent("child");
                var root = new TreeComponent("hi", "", new[] { child });

                var affectedComponent = root.GetByNames<IComponent>(root.Name, child.Name);
                var change = new MessageChangeEvent(
                    DefaultTimestamp,
                    affectedComponent.Path,
                    status,
                    MessageType.Start);

                var result = await Processor.Process(change, EventEntity, root, null);

                Assert.Equal(change.Timestamp, result.Timestamp);
                Assert.Equal(affectedComponent, result.AffectedComponent);
                Assert.Equal(change.AffectedComponentStatus, result.AffectedComponentStatus);

                Factory
                    .Verify(
                        x => x.CreateMessage(
                            EventEntity,
                            DefaultTimestamp,
                            MessageType.Start,
                            affectedComponent),
                        Times.Once());
            }
        }

        public class MessageChangeEventProcessorTest
        {
            public DateTime DefaultTimestamp = new DateTime(2018, 9, 14);
            public EventEntity EventEntity = new EventEntity();

            public Mock<IMessageFactory> Factory { get; }
            public MessageChangeEventProcessor Processor { get; }

            public MessageChangeEventProcessorTest()
            {
                Factory = new Mock<IMessageFactory>();

                Processor = new MessageChangeEventProcessor(
                    Factory.Object,
                    Mock.Of<ILogger<MessageChangeEventProcessor>>());
            }
        }
    }
}
