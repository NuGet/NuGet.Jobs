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
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class EntityFactoryAggregatorTests
    {
        public class TheIncidentEntityCreateMethod 
            : TheCreateMethod<IncidentEntity, IncidentGroupEntity>
        {
        }

        public class TheIncidentGroupEntityCreateMethod 
            : TheCreateMethod<IncidentGroupEntity, EventEntity>
        {
        }
        
        public abstract class TheCreateMethod<TAggregatedEntity, TEntityAggregation>
            : EntityFactoryAggregatorTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task CreatesEntityWithNoAggregation()
            {
                var incident = new Incident() { Source = new IncidentSourceData { CreateDate = new DateTime(2018, 9, 13) } };
                var input = new ParsedIncident(incident, "", ComponentStatus.Degraded);

                Provider
                    .Setup(x => x.GetExistingAggregation(input))
                    .ReturnsAsync((TEntityAggregation)null);

                var aggregation = new TEntityAggregation();
                AggregationFactory
                    .Setup(x => x.Create(input))
                    .ReturnsAsync(aggregation);

                var aggregated = new TAggregatedEntity();
                AggregatedFactory
                    .Setup(x => x.Create(input, aggregation))
                    .ReturnsAsync(aggregated);

                var result = await Factory.Create(input);

                Assert.Equal(aggregated, result);

                LinkListener1
                    .Verify(
                        x => x.OnLink(aggregation, aggregated),
                        Times.Once());

                LinkListener2
                    .Verify(
                        x => x.OnLink(aggregation, aggregated),
                        Times.Once());
            }

            [Fact]
            public async Task CreatesEntityWithExistingAggregation()
            {
                var incident = new Incident() { Source = new IncidentSourceData { CreateDate = new DateTime(2018, 9, 13) } };
                var input = new ParsedIncident(incident, "", ComponentStatus.Degraded);

                var aggregation = new TEntityAggregation();
                Provider
                    .Setup(x => x.GetExistingAggregation(input))
                    .ReturnsAsync(aggregation);

                var aggregated = new TAggregatedEntity();
                AggregatedFactory
                    .Setup(x => x.Create(input, aggregation))
                    .ReturnsAsync(aggregated);

                var result = await Factory.Create(input);

                Assert.Equal(aggregated, result);

                AggregationFactory
                    .Verify(
                        x => x.Create(It.IsAny<ParsedIncident>()),
                        Times.Never());

                LinkListener1
                    .Verify(
                        x => x.OnLink(aggregation, aggregated),
                        Times.Once());

                LinkListener2
                    .Verify(
                        x => x.OnLink(aggregation, aggregated),
                        Times.Once());
            }
        }

        public class EntityFactoryAggregatorTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            public Mock<IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation>> AggregatedFactory { get; }
            public Mock<IEntityFactory<TEntityAggregation>> AggregationFactory { get; }
            public Mock<IExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>> Provider { get; }
            public Mock<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> LinkListener1 { get; }
            public Mock<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>> LinkListener2 { get; }

            public EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation> Factory { get; }

            public EntityFactoryAggregatorTest()
            {
                AggregatedFactory = new Mock<IAggregatedEntityFactory<TAggregatedEntity, TEntityAggregation>>();

                AggregationFactory = new Mock<IEntityFactory<TEntityAggregation>>();

                Provider = new Mock<IExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>>();

                LinkListener1 = new Mock<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>>();

                LinkListener2 = new Mock<IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>>();

                Factory = new EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>(
                    AggregatedFactory.Object,
                    AggregationFactory.Object,
                    Provider.Object,
                    new[] { LinkListener1, LinkListener2 }.Select(l => l.Object),
                    Mock.Of<ILogger<EntityFactoryAggregator<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}
