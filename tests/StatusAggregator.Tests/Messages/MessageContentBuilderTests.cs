// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Messages;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageContentBuilderTests
    {
        public class TheBuildMethodWithImplicitStatus
            : TheBuildMethodTest
        {
            protected override string Invoke(MessageType type, IComponent component, ComponentStatus status)
            {
                return Builder.Build(type, component);
            }

            protected override ComponentStatus GetStatus(IComponent component, ComponentStatus status)
            {
                return component.Status;
            }
        }

        public class TheBuildMethodWithExplicitStatus
            : TheBuildMethodTest
        {
            protected override string Invoke(MessageType type, IComponent component, ComponentStatus status)
            {
                return Builder.Build(type, component, status);
            }

            protected override ComponentStatus GetStatus(IComponent component, ComponentStatus status)
            {
                return status;
            }
        }

        public abstract class TheBuildMethodTest
            : MessageContentBuilderTest
        {
            [Fact]
            public void ThrowsIfMissingTemplateForType()
            {
                var type = MessageType.Manual;
                var component = CreateTestComponent(
                    NuGetServiceComponentFactory.RootName, 
                    NuGetServiceComponentFactory.GalleryName);
                var status = ComponentStatus.Degraded;

                Assert.Throws<ArgumentException>(() => Invoke(type, component, status));
            }

            [Theory]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            public void ThrowsIfMissingActionDescriptionForPath(MessageType type)
            {
                var component = CreateTestComponent("missing");
                var status = ComponentStatus.Degraded;

                Assert.Throws<ArgumentException>(() => Invoke(type, component, status));
            }

            /// <remarks>
            /// In the future, we can be much more explicit about the exact messages expected for each set of inputs.
            /// For now, I think it's more important that we test that each set of inputs returns a value that looks reasonably ok.
            /// </remarks>
            [Theory]
            [ClassData(typeof(BuildsContentsSuccessfully_Data))]
            public void BuildsContentsSuccessfully(MessageType type, IComponent component, ComponentStatus status)
            {
                var result = Invoke(type, component, status);

                Assert.Contains(
                    GetStatus(component, status).ToString().ToLowerInvariant(), 
                    result);

                var names = component.GetNames();
                var expectedName = string.Join(" ", names.Skip(1).Reverse());
                Assert.Contains(expectedName, result);

                if (type == MessageType.Start)
                {
                    Assert.Contains("You may encounter issues", result);
                }
                else if (type == MessageType.End)
                {
                    Assert.Contains("You should no longer encounter any issues", result);
                }
                else
                {
                    throw new ArgumentException(nameof(type));
                }
            }

            protected abstract string Invoke(
                MessageType type,
                IComponent component,
                ComponentStatus status);

            protected abstract ComponentStatus GetStatus(
                IComponent component, 
                ComponentStatus status);
        }

        public class MessageContentBuilderTest
        {
            public MessageContentBuilder Builder { get; }

            public MessageContentBuilderTest()
            {
                Builder = new MessageContentBuilder(
                    Mock.Of<ILogger<MessageContentBuilder>>());
            }
        }

        public static IComponent CreateTestComponent(params string[] names)
        {
            IComponent bottom = null;
            IComponent root = null;
            foreach (var name in names.Reverse())
            {
                if (bottom == null)
                {
                    bottom = new TestComponent(name);
                    root = bottom;
                }
                else
                {
                    root = new TestComponent(name, new[] { root });
                }
            }
            
            return bottom ?? throw new ArgumentException(nameof(names));
        }

        public class BuildsContentsSuccessfully_Data : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                var root = new NuGetServiceComponentFactory().Create();
                foreach (var type in new[] { MessageType.Start, MessageType.End })
                {
                    foreach (var status in new[] { ComponentStatus.Degraded, ComponentStatus.Down })
                    {
                        foreach (var component in root.GetAllVisibleComponents())
                        {
                            if (root == component)
                            {
                                continue;
                            }

                            yield return new object[] { type, component, status };
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
