// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Table;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class IncidentGroupLinkListenerTests
    {
        public class TheOnLinkMethod : IncidentGroupLinkListenerTest
        {
            [Fact]
            public async Task IncreasesSeverity()
            {
                var incident = new IncidentEntity
                {
                    AffectedComponentStatus = 2
                };

                var group = new IncidentGroupEntity
                {
                    AffectedComponentStatus = 1
                };

                await Listener.OnLink(group, incident);

                Assert.Equal(incident.AffectedComponentStatus, group.AffectedComponentStatus);

                Table
                    .Verify(
                        x => x.ReplaceAsync(group),
                        Times.Once());

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Once());
            }
            [Fact]
            public async Task DoesNotIncreasesSeverity()
            {
                var incident = new IncidentEntity
                {
                    AffectedComponentStatus = 1
                };

                var group = new IncidentGroupEntity
                {
                    AffectedComponentStatus = 2
                };

                await Listener.OnLink(group, incident);

                Assert.NotEqual(incident.AffectedComponentStatus, group.AffectedComponentStatus);

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());
            }
        }

        public class IncidentGroupLinkListenerTest
        {
            public Mock<ITableWrapper> Table { get; }
            public IncidentGroupLinkListener Listener { get; }

            public IncidentGroupLinkListenerTest()
            {
                Table = new Mock<ITableWrapper>();

                Listener = new IncidentGroupLinkListener(
                    Table.Object,
                    Mock.Of<ILogger<IncidentGroupLinkListener>>());
            }
        }
    }
}
