// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class EventFactory : IEntityFactory<EventEntity>
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<EventFactory> _logger;

        public EventFactory(
            ITableWrapper table,
            ILogger<EventFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EventEntity> Create(ParsedIncident input)
        {
            var affectedPath = input.AffectedComponentPath;
            using (_logger.Scope("Creating event for parsed incident with path {AffectedComponentPath}.", affectedPath))
            {
                var pathParts = ComponentUtility.GetNames(affectedPath);
                var topLevelComponentPathParts = pathParts.Take(2).ToArray();
                var path = ComponentUtility.GetPath(topLevelComponentPathParts);
                _logger.LogInformation("Creating event for top level component {TopLevelComponentPath}.", path);

                var entity = new EventEntity(path, input.StartTime);
                await _table.InsertOrReplaceAsync(entity);

                return entity;
            }
        }
    }
}
