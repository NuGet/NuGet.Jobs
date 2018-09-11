// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Table;

namespace StatusAggregator.Export
{
    public class ComponentExporter : IComponentExporter
    {
        private readonly ITableWrapper _table;

        private readonly ILogger<ComponentExporter> _logger;

        public ComponentExporter(
            ITableWrapper table,
            ILogger<ComponentExporter> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IComponent Export()
        {
            using (_logger.Scope("Exporting active entities to component."))
            {
                var rootComponent = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent();

                // Apply the active entities to the component tree.
                var activeIncidentGroups = _table
                    .GetActiveEntities<IncidentGroupEntity>()
                    .ToList();

                _logger.LogInformation("Found {GroupCount} active incident groups.", activeIncidentGroups.Count);

                var activeEvents = _table
                    .GetActiveEntities<EventEntity>()
                    .ToList();

                _logger.LogInformation("Found {EventCount} active events.", activeEvents.Count);

                var activeEntities = activeIncidentGroups
                    .Concat<IComponentAffectingEntity>(activeEvents)
                    // Only apply entities with a non-Up status.
                    .Where(e => e.AffectedComponentStatus != (int)ComponentStatus.Up)
                    // If multiple events are affecting a single region, the event with the highest severity should affect the component.
                    .GroupBy(e => e.AffectedComponentPath)
                    .Select(g => g.OrderByDescending(e => e.AffectedComponentStatus).First())
                    .ToList();

                _logger.LogInformation("Active entities affect {PathCount} distinct subcomponents.", activeEntities.Count);
                foreach (var activeEntity in activeEntities)
                {
                    using (_logger.Scope("Applying active entity affecting '{AffectedComponentPath}' of severity {AffectedComponentStatus} at {StartTime} to root component",
                        activeEntity.AffectedComponentPath, activeEntity.AffectedComponentStatus, activeEntity.StartTime))
                    {
                        var currentComponent = rootComponent.GetByPath(activeEntity.AffectedComponentPath);

                        if (currentComponent == null)
                        {
                            _logger.LogWarning("Couldn't find component corresponding to active entities.");
                            continue;
                        }

                        currentComponent.Status = (ComponentStatus)activeEntity.AffectedComponentStatus;
                    }
                }

                return rootComponent;
            }
        }
    }
}
