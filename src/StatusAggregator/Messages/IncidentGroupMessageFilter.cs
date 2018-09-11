// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Does not post messages about an <see cref="IncidentGroupEntity"/> unless it has incidents that have been active for a specified delay.
    /// </summary>
    public class IncidentGroupMessageFilter : IIncidentGroupMessageFilter
    {
        private readonly TimeSpan _eventStartMessageDelay;

        private readonly ITableWrapper _table;

        private readonly ILogger<IncidentGroupMessageFilter> _logger;

        public IncidentGroupMessageFilter(
            ITableWrapper table,
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentGroupMessageFilter> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventStartMessageDelay = TimeSpan.FromMinutes(configuration?.EventStartMessageDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool CanPostMessages(IncidentGroupEntity group)
        {
            var linkedIncidentsQuery = _table.GetLinkedEntities<IncidentEntity, IncidentGroupEntity>(group);

            var activeIncidents = linkedIncidentsQuery
                .Where(i => i.IsActive)
                .ToList();

            _logger.LogInformation("Incident group is linked to {ActiveIncidentsCount} active incidents.", 
                activeIncidents.Count);

            var incidentsActiveAfterDelay = linkedIncidentsQuery
                .Where(i => i.EndTime > group.StartTime + _eventStartMessageDelay)
                .ToList();

            _logger.LogInformation("Incident group is linked to {ActiveIncidentsCount} incidents that were active after the messaging delay.", 
                incidentsActiveAfterDelay.Count);

            var hasBeenActiveLongerThanDelay = activeIncidents.Any() || incidentsActiveAfterDelay.Any();
            return hasBeenActiveLongerThanDelay;
        }
    }
}
