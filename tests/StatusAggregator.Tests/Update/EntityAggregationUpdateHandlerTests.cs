// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class EntityAggregationUpdateHandlerTests
    {
        private const string RowKey = "rowkey";
        private const int EventEndDelayMinutes = 5;
        private static readonly TimeSpan EventEndDelay = TimeSpan.FromMinutes(EventEndDelayMinutes);
        private static readonly DateTime NextCreationTime = new DateTime(2018, 7, 10);

        private static IEnumerable<IncidentEntity> ClosableIncidents => new[]
        {
            CreateIncidentEntity(new DateTime(2018, 7, 9)), // Recently closed incident
            CreateIncidentEntity(DateTime.MinValue), // Old incident
        };

        private static IEnumerable<IncidentEntity> UnclosableIncidents => new[]
        {
            CreateIncidentEntity(NextCreationTime + EventEndDelay), // Incident closed too recently
            CreateIncidentEntity() // Active incident
        };

        private Mock<ITableWrapper> _tableWrapperMock { get; }
        private Mock<IComponentAffectingEntityUpdater<IncidentEntity>> _aggregatedEntityUpdater { get; }
        private EntityAggregationUpdateHandler<IncidentEntity, IncidentGroupEntity> _updater { get; }
        private IncidentGroupEntity _eventEntity { get; }

        public EntityAggregationUpdateHandlerTests()
        {
            var configuration = new StatusAggregatorConfiguration()
            {
                EventEndDelayMinutes = EventEndDelayMinutes
            };

            _tableWrapperMock = new Mock<ITableWrapper>();

            _aggregatedEntityUpdater = new Mock<IComponentAffectingEntityUpdater<IncidentEntity>>();
            foreach (var incident in ClosableIncidents)
            {
                _aggregatedEntityUpdater
                    .Setup(x => x.Update(incident, NextCreationTime))
                    .Returns(Task.FromResult(true));
            }

            foreach (var incident in UnclosableIncidents)
            {
                _aggregatedEntityUpdater
                    .Setup(x => x.Update(incident, NextCreationTime))
                    .Returns(Task.FromResult(false));
            }

            _updater = new EntityAggregationUpdateHandler<IncidentEntity, IncidentGroupEntity>(
                _tableWrapperMock.Object,
                _aggregatedEntityUpdater.Object,
                configuration, 
                Mock.Of<ILogger<EntityAggregationUpdateHandler<IncidentEntity, IncidentGroupEntity>>>());

            _eventEntity = new IncidentGroupEntity()
            {
                RowKey = RowKey,
                StartTime = DateTime.MinValue,
                EndTime = null
            };
        }

        [Fact]
        public async Task ThrowsIfEventNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _updater.Update(null, DateTime.MinValue));
        }

        [Fact]
        public async Task ReturnsTrueIfNotActive()
        {
            _eventEntity.EndTime = DateTime.MinValue;

            var result = await _updater.Update(_eventEntity, NextCreationTime);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsFalseIfNoLinkedIncidents()
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(new IncidentEntity[0].AsQueryable());

            var result = await _updater.Update(_eventEntity, NextCreationTime);

            Assert.False(result);
            _aggregatedEntityUpdater.Verify(
                x => x.Update(It.IsAny<IncidentEntity>(), It.IsAny<DateTime>()), 
                Times.Never());
        }

        public static IEnumerable<object[]> DoesNotCloseEventIfUnclosableIncident_Data
        {
            get
            {
                foreach (var unclosableIncident in UnclosableIncidents)
                {
                    yield return new object[] { unclosableIncident };
                }
            }
        }

        [Theory]
        [MemberData(nameof(DoesNotCloseEventIfUnclosableIncident_Data))]
        public async Task DoesNotCloseEventIfUnclosableIncident(IncidentEntity unclosableIncident)
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(ClosableIncidents.Concat(new[] { unclosableIncident }).AsQueryable());

            var result = await _updater.Update(_eventEntity, NextCreationTime);

            Assert.False(result);
            Assert.Null(_eventEntity.EndTime);
            foreach (var incident in _tableWrapperMock.Object.CreateQuery<IncidentEntity>().ToList())
            {
                _aggregatedEntityUpdater.Verify(
                    x => x.Update(incident, NextCreationTime),
                    Times.Once());
            }
        }

        [Fact]
        public async Task ClosesEventIfClosableIncidents()
        {
            _tableWrapperMock
                .Setup(x => x.CreateQuery<IncidentEntity>())
                .Returns(ClosableIncidents.AsQueryable());

            var result = await _updater.Update(_eventEntity, NextCreationTime);

            var expectedEndTime = ClosableIncidents.Max(i => i.EndTime ?? DateTime.MinValue);
            Assert.True(result);
            Assert.Equal(expectedEndTime, _eventEntity.EndTime);
            _tableWrapperMock.Verify(
                x => x.InsertOrReplaceAsync(_eventEntity),
                Times.Once());
            foreach (var incident in _tableWrapperMock.Object.CreateQuery<IncidentEntity>().ToList())
            {
                _aggregatedEntityUpdater.Verify(
                    x => x.Update(incident, NextCreationTime),
                    Times.Once());
            }
        }

        private static IncidentEntity CreateIncidentEntity(DateTime? endTime = null)
        {
            return new IncidentEntity()
            {
                PartitionKey = IncidentEntity.DefaultPartitionKey,
                ParentRowKey = RowKey,
                StartTime = DateTime.MinValue,
                EndTime = endTime
            };
        }
    }
}
