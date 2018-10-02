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
        private readonly IActiveEventEntityUpdater _activeEventUpdater;

        private readonly ILogger<StatusUpdater> _logger;

        public StatusUpdater(
            ICursor cursor,
            IEnumerable<IEntityCollector> collectors,
            IActiveEventEntityUpdater activeEventUpdater,
            ILogger<StatusUpdater> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            collectors = collectors?.Where(c => c != null) 
                ?? throw new ArgumentNullException(nameof(collectors));
            _incidentCollector = collectors.SingleOrDefault(IsIncidentCollector) 
                ?? throw new ArgumentException(nameof(collectors), $"Must provide a collector with name {IncidentEntityCollectorProcessor.IncidentsCollectorName}!");
            _manualStatusChangeCollectors = collectors.Where(c => !IsIncidentCollector(c));
            _activeEventUpdater = activeEventUpdater ?? throw new ArgumentNullException(nameof(activeEventUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Update(DateTime cursor)
        {
            using (_logger.Scope("Updating service status."))
            {
                foreach (var manualStatusChangeCollector in _manualStatusChangeCollectors)
                {
                    await manualStatusChangeCollector.FetchLatest();
                }

                await _incidentCollector.FetchLatest();
                await _activeEventUpdater.UpdateAll(cursor);
            }
        }

        private static bool IsIncidentCollector(IEntityCollector collector)
        {
            return collector.Name == IncidentEntityCollectorProcessor.IncidentsCollectorName;
        }
    }
}