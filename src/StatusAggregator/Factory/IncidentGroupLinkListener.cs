// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class IncidentGroupLinkListener : IEntityAggregationLinkListener<IncidentEntity, IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<IncidentGroupLinkListener> _logger;

        public IncidentGroupLinkListener(
            ITableWrapper table,
            ILogger<IncidentGroupLinkListener> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnLink(IncidentGroupEntity groupEntity, IncidentEntity incidentEntity)
        {
            if (incidentEntity.AffectedComponentStatus > groupEntity.AffectedComponentStatus)
            {
                _logger.LogInformation("Incident {IncidentRowKey} has a greater severity than incident group {GroupRowKey} it was just linked to ({NewSeverity} > {OldSeverity}), updating group's severity.",
                    incidentEntity.RowKey, groupEntity.RowKey, (ComponentStatus)incidentEntity.AffectedComponentStatus, (ComponentStatus)groupEntity.AffectedComponentStatus);
                groupEntity.AffectedComponentStatus = incidentEntity.AffectedComponentStatus;
                await _table.ReplaceAsync(groupEntity);
            }
        }
    }
}
