// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class EventFactory : IEntityFactory<EventEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IAggregationPathProvider<IncidentGroupEntity, EventEntity> _pathProvider;

        private readonly ILogger<EventFactory> _logger;

        public EventFactory(
            ITableWrapper table,
            IAggregationPathProvider<IncidentGroupEntity, EventEntity> pathProvider,
            ILogger<EventFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EventEntity> Create(ParsedIncident input)
        {
            var affectedPath = input.AffectedComponentPath;
            using (_logger.Scope("Creating event for parsed incident with path {AffectedComponentPath}.", affectedPath))
            {
                var path = _pathProvider.Get(input);
                _logger.LogInformation("Creating event for top level component {TopLevelComponentPath}.", path);

                var entity = new EventEntity(path, input.StartTime);
                await _table.InsertOrReplaceAsync(entity);

                return entity;
            }
        }
    }
}
