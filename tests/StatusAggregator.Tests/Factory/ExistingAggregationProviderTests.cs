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
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class ExistingAggregationProviderTests
    {
        public class TheIncidentEntityGetExistingAggregationMethod
            : TheGetExistingAggregationMethod<IncidentEntity, IncidentGroupEntity>
        {
        }
        public class TheIncidentGroupEntityGetExistingAggregationMethod
            : TheGetExistingAggregationMethod<IncidentGroupEntity, EventEntity>
        {
        }

        public abstract class TheGetExistingAggregationMethod<TAggregatedEntity, TEntityAggregation>
            : ExistingAggregationProviderTest<TAggregatedEntity, TEntityAggregation>
            where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
            where TEntityAggregation : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task HandlesSinglePartPathWithNoPossibleAggregation()
            {
                var inputPath = "howdy";
                var input = new ParsedIncident(Incident, inputPath, ComponentStatus.Degraded);
                
                var providedPath = "hello";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(providedPath);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var aggregations = new[]
                {
                    aggregationWithDifferentPath,
                    aggregationAfter,
                    aggregationBefore,
                    activeAggregationToDeactivate,
                    inactiveAggregationToDeactivate
                };

                Table
                    .Setup(x => x.CreateQuery<TEntityAggregation>())
                    .Returns(aggregations.AsQueryable());

                Handler
                    .Setup(x => x.CanLink(input, activeAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, inactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Null(result);

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), activeAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), inactiveAggregationToDeactivate),
                        Times.Once());
            }

            [Fact]
            public async Task HandlesSinglePartPathWithPossibleAggregation()
            {
                var inputPath = "howdy";
                var input = new ParsedIncident(Incident, inputPath, ComponentStatus.Degraded);

                var providedPath = "hello";
                PathProvider
                    .Setup(x => x.Get(input))
                    .Returns(providedPath);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var activeAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = providedPath,
                    StartTime = input.StartTime
                };

                var aggregations = new[]
                {
                    aggregationWithDifferentPath,
                    aggregationAfter,
                    aggregationBefore,
                    activeAggregationToDeactivate,
                    inactiveAggregationToDeactivate,
                    activeAggregation
                };

                Table
                    .Setup(x => x.CreateQuery<TEntityAggregation>())
                    .Returns(aggregations.AsQueryable());

                Handler
                    .Setup(x => x.CanLink(input, activeAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, inactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, activeAggregation))
                    .ReturnsAsync(true);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Equal(activeAggregation, result);

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), activeAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), inactiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), activeAggregation),
                        Times.Once());
            }
        }

        public class ExistingAggregationProviderTest<TAggregatedEntity, TEntityAggregation>
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

            public Mock<ITableWrapper> Table { get; }
            public Mock<IAggregationPathProvider<TAggregatedEntity, TEntityAggregation>> PathProvider { get; }
            public Mock<IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>> Handler { get; }
            public ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation> Provider { get; }

            public ExistingAggregationProviderTest()
            {
                Table = new Mock<ITableWrapper>();

                PathProvider = new Mock<IAggregationPathProvider<TAggregatedEntity, TEntityAggregation>>();

                Handler = new Mock<IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>>();

                Provider = new ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>(
                    Table.Object,
                    PathProvider.Object,
                    Handler.Object,
                    Mock.Of<ILogger<ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}
