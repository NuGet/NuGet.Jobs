// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class IncidentGroupFactory : IAggregatedEntityFactory<IncidentGroupEntity, EventEntity>
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<IncidentGroupFactory> _logger;

        public IncidentGroupFactory(
            ITableWrapper table,
            ILogger<IncidentGroupFactory> logger)
        {
            _table = table;
            _logger = logger;
        }

        public async Task<IncidentGroupEntity> Create(ParsedIncident input, EventEntity eventEntity)
        {
            var affectedPath = input.AffectedComponentPath;
            using (_logger.Scope("Creating incident for parsed incident with path {AffectedComponentPath}.", affectedPath))
            {
                var entity = new IncidentGroupEntity(
                    eventEntity,
                    affectedPath,
                    (ComponentStatus)input.AffectedComponentStatus,
                    input.StartTime);

                await _table.InsertOrReplaceAsync(entity);

                return entity;
            }
        }
    }
}
