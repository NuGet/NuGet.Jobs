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
    public class IncidentFactory : IAggregatedEntityFactory<IncidentEntity, IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<IncidentFactory> _logger;

        public IncidentFactory(
            ITableWrapper table,
            ILogger<IncidentFactory> logger)
        {
            _table = table;
            _logger = logger;
        }

        public async Task<IncidentEntity> Create(ParsedIncident input, IncidentGroupEntity groupEntity)
        {
            var affectedPath = input.AffectedComponentPath;
            using (_logger.Scope("Creating incident for parsed incident with path {AffectedComponentPath}.", affectedPath))
            {
                var entity = new IncidentEntity(
                    input.Id,
                    groupEntity,
                    affectedPath,
                    (ComponentStatus)input.AffectedComponentStatus,
                    input.StartTime,
                    input.EndTime);

                await _table.InsertOrReplaceAsync(entity);

                return entity;
            }
        }
    }
}
