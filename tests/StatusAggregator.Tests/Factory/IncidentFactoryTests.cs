// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
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
    public class IncidentFactoryTests
    {
        public class TheCreateMethod : IncidentFactoryTest
        {
            [Fact]
            public async Task CreatesEntity()
            {
                var input = new ParsedIncident(Incident, "the path", ComponentStatus.Degraded);

                var group = new IncidentGroupEntity
                {
                    RowKey = "parentRowKey"
                };

                IncidentEntity entity = null;
                Table
                    .Setup(x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ITableEntity>(e =>
                    {
                        Assert.IsType<IncidentEntity>(e);
                        entity = e as IncidentEntity;
                    });

                var result = await Factory.Create(input, group);

                Assert.Equal(entity, result);

                Assert.Equal(input.Id, entity.IncidentApiId);
                Assert.Equal(group.RowKey, entity.ParentRowKey);
                Assert.Equal(input.AffectedComponentPath, entity.AffectedComponentPath);
                Assert.Equal(input.AffectedComponentStatus, entity.AffectedComponentStatus);
                Assert.Equal(input.StartTime, entity.StartTime);
                Assert.Equal(input.EndTime, entity.EndTime);

                Table
                    .Verify(
                        x => x.InsertOrReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Once());
            }
        }

        public class IncidentFactoryTest
        {
            public Incident Incident = new Incident()
            {
                Id = "some ID",

                Source = new IncidentSourceData
                {
                    CreateDate = new DateTime(2018, 9, 13)
                },

                MitigationData = new IncidentStateChangeEventData
                {
                    Date = new DateTime(2018, 9, 14)
                }
            };

            public Mock<ITableWrapper> Table { get; }
            public IncidentFactory Factory { get; }

            public IncidentFactoryTest()
            {
                Table = new Mock<ITableWrapper>();

                Factory = new IncidentFactory(
                    Table.Object,
                    Mock.Of<ILogger<IncidentFactory>>());
            }
        }
    }
}
