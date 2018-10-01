// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class ExistingAggregationLinkHandlerTests
    {
        public class TheIncidentEntityCanLinkMethod 
            : TheCanLinkMethod<IncidentEntity, IncidentGroupEntity>
        {
        }

        public class TheIncidentGroupEntityCanLinkMethod 
            : TheCanLinkMethod<IncidentGroupEntity, EventEntity>
        {
        }

        public abstract class TheCanLinkMethod<TAggregatedEntity, TEntityAggregation>
            : ExistingAggregationLinkHandlerTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task ReturnsFalseIfNoLinkedEntities()
            {
                var aggregatedEntityLinkedToDifferentAggregation = new TAggregatedEntity
                {
                    ParentRowKey = "wrongRowKey"
                };

                Table
                    .Setup(x => x.CreateQuery<TAggregatedEntity>())
                    .Returns(new[] { aggregatedEntityLinkedToDifferentAggregation }.AsQueryable());

                var result = await Handler.CanLink(Input, Aggregation);

                Assert.False(result);

                Updater
                    .Verify(
                        x => x.Update(It.IsAny<TEntityAggregation>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task ReturnsFalseIfUpdaterReturnsTrue()
            {
                var aggregatedEntity = new TAggregatedEntity
                {
                    ParentRowKey = AggregationRowKey
                };

                Table
                    .Setup(x => x.CreateQuery<TAggregatedEntity>())
                    .Returns(new[] { aggregatedEntity }.AsQueryable());

                Updater
                    .Setup(x => x.Update(Aggregation, Input.StartTime))
                    .ReturnsAsync(true);

                var result = await Handler.CanLink(Input, Aggregation);

                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsTrueIfLinkedEntitiesAndUpdaterReturnsFalse()
            {
                var aggregatedEntity = new TAggregatedEntity
                {
                    ParentRowKey = AggregationRowKey
                };

                Table
                    .Setup(x => x.CreateQuery<TAggregatedEntity>())
                    .Returns(new[] { aggregatedEntity }.AsQueryable());

                Updater
                    .Setup(x => x.Update(Aggregation, Input.StartTime))
                    .ReturnsAsync(false);

                var result = await Handler.CanLink(Input, Aggregation);

                Assert.True(result);
            }
        }

        public class ExistingAggregationLinkHandlerTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            public Incident Incident = new Incident()
            {
                Source = new IncidentSourceData()
                {
                    CreateDate = new DateTime(2018, 9, 13)
                }
            };

            public ParsedIncident Input { get; }

            public const string AggregationRowKey = "aggregationRowKey";
            public TEntityAggregation Aggregation = new TEntityAggregation()
            {
                RowKey = AggregationRowKey
            };

            public Mock<ITableWrapper> Table { get; }
            public Mock<IComponentAffectingEntityUpdater<TEntityAggregation>> Updater { get; }
            public ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation> Handler { get; }

            public ExistingAggregationLinkHandlerTest()
            {
                Input = new ParsedIncident(Incident, "", ComponentStatus.Up);

                Table = new Mock<ITableWrapper>();

                Updater = new Mock<IComponentAffectingEntityUpdater<TEntityAggregation>>();

                Handler = new ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>(
                    Table.Object,
                    Updater.Object,
                    Mock.Of<ILogger<ExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}
