// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using StatusAggregator.Collector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator.Update
{
    public class StatusUpdater : IStatusUpdater
    {
        private const string ManualCursorBaseName = "manual";
        private const string IncidentCursorName = "incident";

        private readonly ICursor _cursor;
        private readonly IEntityCollector _incidentCollector;
        private readonly IEnumerable<IEntityCollector> _manualStatusChangeCollectors;
        private readonly IEnumerable<IComponentAffectingEntityUpdater> _aggregationUpdaters;

        private readonly ILogger<StatusUpdater> _logger;

        public StatusUpdater(
            ICursor cursor,
            IEnumerable<IEntityCollector> collectors,
            IEnumerable<IComponentAffectingEntityUpdater> aggregationUpdaters,
            ILogger<StatusUpdater> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));

            collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
            _incidentCollector = collectors.Single(IsIncidentCollector);
            _manualStatusChangeCollectors = collectors.Where(c => !IsIncidentCollector(c));

            _aggregationUpdaters = aggregationUpdaters ?? throw new ArgumentNullException(nameof(aggregationUpdaters));
            if (!_aggregationUpdaters.Any())
            {
                throw new ArgumentException(nameof(aggregationUpdaters));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Update()
        {
            using (_logger.Scope("Updating service status."))
            {
                foreach (var manualStatusChangeCollector in _manualStatusChangeCollectors)
                {
                    await manualStatusChangeCollector.FetchLatest();
                }

                await _incidentCollector.FetchLatest();

                // Update the active entities based on current time.
                // If there are no incidents in a while, the incidents cursor won't reflect current time.
                var currentTime = DateTime.UtcNow;
                foreach (var aggregationUpdater in _aggregationUpdaters)
                {
                    await aggregationUpdater.UpdateAllActive(currentTime);
                }
            }
        }

        private bool IsIncidentCollector(IEntityCollector collector)
        {
            return collector.Name == IncidentEntityCollectorProcessor.IncidentsCollectorName;
        }
    }
}
