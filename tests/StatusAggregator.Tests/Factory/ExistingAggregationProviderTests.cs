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
            public async Task GracefullyHandlesEmptyPath()
            {
                var input = new ParsedIncident(Incident, string.Empty, ComponentStatus.Degraded);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Null(result);

                Table
                    .Verify(
                        x => x.CreateQuery<TEntityAggregation>(),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), It.IsAny<TEntityAggregation>()),
                        Times.Never());
            }

            [Fact]
            public async Task HandlesSinglePartPathWithNoPossibleAggregation()
            {
                var path = "hello";
                var input = new ParsedIncident(Incident, path, ComponentStatus.Degraded);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = path,
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
                var path = "hello";
                var input = new ParsedIncident(Incident, path, ComponentStatus.Degraded);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var aggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var aggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var activeAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime
                };

                var inactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = path,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var activeAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = path,
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

            [Fact]
            public async Task HandlesTwoPartPathWithSinglePartPossibleAggregation()
            {
                var firstPart = "one";
                var path = firstPart + Constants.ComponentPathDivider + "two";
                var input = new ParsedIncident(Incident, path, ComponentStatus.Degraded);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var firstPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var firstPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var firstPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var firstPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var firstPartActiveAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var secondPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var secondPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var secondPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var secondPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var secondPartActiveAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var aggregations = new[]
                {
                    aggregationWithDifferentPath,
                    firstPartAggregationAfter,
                    firstPartAggregationBefore,
                    firstPartActiveAggregationToDeactivate,
                    firstPartInactiveAggregationToDeactivate,
                    firstPartActiveAggregation,
                    secondPartAggregationAfter,
                    secondPartAggregationBefore,
                    secondPartActiveAggregationToDeactivate,
                    secondPartInactiveAggregationToDeactivate,
                    secondPartActiveAggregation
                };

                Table
                    .Setup(x => x.CreateQuery<TEntityAggregation>())
                    .Returns(aggregations.AsQueryable());

                Handler
                    .Setup(x => x.CanLink(input, firstPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, firstPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, firstPartActiveAggregation))
                    .ReturnsAsync(true);

                Handler
                    .Setup(x => x.CanLink(input, secondPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartActiveAggregation))
                    .ReturnsAsync(true);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Equal(firstPartActiveAggregation, result);

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartActiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartInactiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartActiveAggregation),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartActiveAggregationToDeactivate),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartInactiveAggregationToDeactivate),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartActiveAggregation),
                        Times.Never());
            }

            [Fact]
            public async Task HandlesTwoPartPathWithTwoPartPossibleAggregation()
            {
                var firstPart = "one";
                var path = firstPart + Constants.ComponentPathDivider + "two";
                var input = new ParsedIncident(Incident, path, ComponentStatus.Degraded);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var firstPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var firstPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var firstPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var firstPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var secondPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var secondPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var secondPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var secondPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var secondPartActiveAggregation = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var aggregations = new[]
                {
                    aggregationWithDifferentPath,
                    firstPartAggregationAfter,
                    firstPartAggregationBefore,
                    firstPartActiveAggregationToDeactivate,
                    firstPartInactiveAggregationToDeactivate,
                    secondPartAggregationAfter,
                    secondPartAggregationBefore,
                    secondPartActiveAggregationToDeactivate,
                    secondPartInactiveAggregationToDeactivate,
                    secondPartActiveAggregation
                };

                Table
                    .Setup(x => x.CreateQuery<TEntityAggregation>())
                    .Returns(aggregations.AsQueryable());

                Handler
                    .Setup(x => x.CanLink(input, firstPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, firstPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartActiveAggregation))
                    .ReturnsAsync(true);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Equal(secondPartActiveAggregation, result);

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartActiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartInactiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartActiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartInactiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartActiveAggregation),
                        Times.Once());
            }

            [Fact]
            public async Task HandlesTwoPartPathWithoutPossibleAggregation()
            {
                var firstPart = "one";
                var path = firstPart + Constants.ComponentPathDivider + "two";
                var input = new ParsedIncident(Incident, path, ComponentStatus.Degraded);

                var aggregationWithDifferentPath = new TEntityAggregation
                {
                    AffectedComponentPath = "other path",
                    StartTime = input.StartTime
                };

                var firstPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var firstPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var firstPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var firstPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var secondPartAggregationAfter = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime + TimeSpan.FromDays(1)
                };

                var secondPartAggregationBefore = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime - TimeSpan.FromDays(2),
                    EndTime = input.StartTime - TimeSpan.FromDays(1)
                };

                var secondPartActiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime
                };

                var secondPartInactiveAggregationToDeactivate = new TEntityAggregation
                {
                    AffectedComponentPath = firstPart,
                    StartTime = input.StartTime,
                    EndTime = input.StartTime
                };

                var aggregations = new[]
                {
                    aggregationWithDifferentPath,
                    firstPartAggregationAfter,
                    firstPartAggregationBefore,
                    firstPartActiveAggregationToDeactivate,
                    firstPartInactiveAggregationToDeactivate,
                    secondPartAggregationAfter,
                    secondPartAggregationBefore,
                    secondPartActiveAggregationToDeactivate,
                    secondPartInactiveAggregationToDeactivate
                };

                Table
                    .Setup(x => x.CreateQuery<TEntityAggregation>())
                    .Returns(aggregations.AsQueryable());

                Handler
                    .Setup(x => x.CanLink(input, firstPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, firstPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartActiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                Handler
                    .Setup(x => x.CanLink(input, secondPartInactiveAggregationToDeactivate))
                    .ReturnsAsync(false);

                var result = await Provider.GetExistingAggregation(input);

                Assert.Null(result);

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), aggregationWithDifferentPath),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartActiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), firstPartInactiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationAfter),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartAggregationBefore),
                        Times.Never());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartActiveAggregationToDeactivate),
                        Times.Once());

                Handler
                    .Verify(
                        x => x.CanLink(It.IsAny<ParsedIncident>(), secondPartInactiveAggregationToDeactivate),
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
            public Mock<IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>> Handler { get; }
            public ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation> Provider { get; }

            public ExistingAggregationProviderTest()
            {
                Table = new Mock<ITableWrapper>();

                Handler = new Mock<IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation>>();

                Provider = new ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>(
                    Table.Object,
                    Handler.Object,
                    Mock.Of<ILogger<ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>>>());
            }
        }
    }
}
