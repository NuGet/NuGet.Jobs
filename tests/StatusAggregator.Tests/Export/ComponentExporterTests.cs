// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using StatusAggregator.Factory;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class ComponentExporterTests
    {
        public class TheExportMethod : ComponentExporterTest
        {
            [Fact]
            public void ReturnsUnaffectedComponentTreeWithNoEntities()
            {
                SetupTableQuery<MessageEntity>();
                SetupTableQuery<IncidentGroupEntity>();
                SetupTableQuery<EventEntity>();

                var result = Exporter.Export();

                Assert.All(
                    result.GetAllComponents(), 
                    c => Assert.Equal(ComponentStatus.Up, c.Status));
            }

            [Fact]
            public void AppliesActiveEntitiesToComponentTree()
            {
                var eventWithMessage = new EventEntity(Level2A.Path, DefaultStartTime, ComponentStatus.Degraded);
                var message = new MessageEntity(eventWithMessage, DefaultStartTime, "", MessageType.Manual);
                var incidentGroup1EventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Degraded, DefaultStartTime);
                var incidentGroup2EventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Down, DefaultStartTime);
                var incidentGroup3EventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3AFrom2A.Path, ComponentStatus.Up, DefaultStartTime, DefaultStartTime);
                var incidentGroup4EventWithMessage = new IncidentGroupEntity(eventWithMessage, Level3BFrom2A.Path, ComponentStatus.Degraded, DefaultStartTime, DefaultStartTime);

                // Does not throw if active entity with missing path is applied.
                var incidentGroup5EventWithMessage = new IncidentGroupEntity(eventWithMessage, "missingPath", ComponentStatus.Degraded, DefaultStartTime, DefaultStartTime);

                var eventWithoutMessage = new EventEntity(Level2B.Path, DefaultStartTime, ComponentStatus.Degraded);
                var incidentGroup1EventWithoutMessage = new IncidentGroupEntity(eventWithoutMessage, Level3AFrom2B.Path, ComponentStatus.Degraded, DefaultStartTime);
                var incidentGroup2EventWithoutMessage = new IncidentGroupEntity(eventWithoutMessage, Level3BFrom2B.Path, ComponentStatus.Degraded, DefaultStartTime);

                SetupTableQuery(new[] { message });
                SetupTableQuery(new[] {
                    incidentGroup1EventWithMessage, incidentGroup2EventWithMessage, incidentGroup3EventWithMessage, incidentGroup4EventWithMessage, incidentGroup5EventWithMessage,
                    incidentGroup1EventWithoutMessage, incidentGroup2EventWithoutMessage });
                SetupTableQuery(new[] { eventWithMessage, eventWithoutMessage });

                var result = Exporter.Export();

                // Status of events with messages are applied to the component.
                Assert.Equal(ComponentStatus.Degraded, Level2A.Status);

                // Status of incident groups linked to events with messages are applied to the component.
                // The most severe status is applied.
                Assert.Equal(ComponentStatus.Down, Level3AFrom2A.Status);

                // Status of incident groups that are not active are not applied.
                Assert.Equal(ComponentStatus.Up, Level3BFrom2A.Status);

                // Status of events without messages are not applied to the component.
                // Status of incident groups linked to events without messages are not applied to the component.
                Assert.All(
                    Level2B.GetAllComponents(),
                    c => Assert.Equal(ComponentStatus.Up, c.Status));
            }
        }

        public class ComponentExporterTest
        {
            public DateTime DefaultStartTime => new DateTime(2018, 9, 12);

            public IComponent Root { get; }
            public IComponent Level2A { get; }
            public IComponent Level2B { get; }
            public IComponent Level3AFrom2A { get; }
            public IComponent Level3BFrom2A { get; }
            public IComponent Level3AFrom2B { get; }
            public IComponent Level3BFrom2B { get; }

            public Mock<IComponentFactory> Factory { get; }
            public Mock<ITableWrapper> Table { get; }
            public ComponentExporter Exporter { get; }

            public ComponentExporterTest()
            {
                var level3AFrom2A = new TestComponent("3A");
                var level3BFrom2A = new TestComponent("3B");
                var level2A = new TestComponent("2A", 
                    new[] { level3AFrom2A, level3BFrom2A });

                var level3AFrom2B = new TestComponent("3A");
                var level3BFrom2B = new TestComponent("3B");
                var level2B = new TestComponent("2B", 
                    new[] { level3AFrom2B, level3BFrom2B });

                Root = new TestComponent("Root", new[] { level2A, level2B });

                // We have to get the subcomponents by iterating through the tree. 
                // Components only have a path in the context of accessing them through a parent.
                Level2A = Root
                    .SubComponents.Single(c => c.Name == "2A");
                Level3AFrom2A = Root
                    .SubComponents.Single(c => c.Name == "2A")
                    .SubComponents.Single(c => c.Name == "3A");
                Level3BFrom2A = Root
                    .SubComponents.Single(c => c.Name == "2A")
                    .SubComponents.Single(c => c.Name == "3B");

                Level2B = Root
                    .SubComponents.Single(c => c.Name == "2B");
                Level3AFrom2B = Root
                    .SubComponents.Single(c => c.Name == "2B")
                    .SubComponents.Single(c => c.Name == "3A");
                Level3BFrom2B = Root
                    .SubComponents.Single(c => c.Name == "2B")
                    .SubComponents.Single(c => c.Name == "3B");

                Factory = new Mock<IComponentFactory>();
                Factory
                    .Setup(x => x.Create())
                    .Returns(Root);

                Table = new Mock<ITableWrapper>();

                Exporter = new ComponentExporter(
                    Table.Object,
                    Factory.Object,
                    Mock.Of<ILogger<ComponentExporter>>());
            }

            public void SetupTableQuery<T>(IEnumerable<T> entities)
                where T : ITableEntity, new()
            {
                Table
                    .Setup(x => x.CreateQuery<T>())
                    .Returns(entities.AsQueryable());
            }

            public void SetupTableQuery<T>()
                where T : ITableEntity, new()
            {
                SetupTableQuery(Enumerable.Empty<T>());
            }
        }
    }
}
