// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentEntityUpdateHandler : IComponentAffectingEntityUpdateHandler<IncidentEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IIncidentApiClient _incidentApiClient;
        private readonly ILogger<IncidentEntityCollectorProcessor> _logger;

        public IncidentEntityUpdateHandler(
            ITableWrapper table,
            IIncidentApiClient incidentApiClient,
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentEntityCollectorProcessor> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _incidentApiClient = incidentApiClient ?? throw new ArgumentNullException(nameof(incidentApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> Update(IncidentEntity entity, DateTime cursor)
        {
            var activeIncident = await _incidentApiClient.GetIncident(entity.IncidentApiId);
            var endTime = activeIncident.MitigationData?.Date;

            if (entity.EndTime != endTime)
            {
                _logger.LogInformation("Updated mitigation time of active incident to {MitigationTime}", entity.EndTime);
                entity.EndTime = endTime;
                await _table.InsertOrReplaceAsync(entity);
            }

            return entity.EndTime != null;
        }
    }
}
