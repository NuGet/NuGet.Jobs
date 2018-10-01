﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class IncidentGroupFactory : IComponentAffectingEntityFactory<IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IAggregationProvider<IncidentGroupEntity, EventEntity> _aggregationProvider;

        private readonly ILogger<IncidentGroupFactory> _logger;

        public IncidentGroupFactory(
            ITableWrapper table,
            IAggregationProvider<IncidentGroupEntity, EventEntity> aggregationProvider,
            ILogger<IncidentGroupFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationProvider = aggregationProvider ?? throw new ArgumentNullException(nameof(aggregationProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IncidentGroupEntity> Create(ParsedIncident input)
        {
            var eventEntity = await _aggregationProvider.Get(input);

            var affectedPath = input.AffectedComponentPath;
            using (_logger.Scope("Creating incident for parsed incident with path {AffectedComponentPath}.", affectedPath))
            {
                var incidentGroupEntity = new IncidentGroupEntity(
                    eventEntity,
                    affectedPath,
                    (ComponentStatus)input.AffectedComponentStatus,
                    input.StartTime);

                await _table.InsertOrReplaceAsync(incidentGroupEntity);

                return incidentGroupEntity;
            }
        }
    }
}
