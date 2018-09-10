// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Export
{
    public class EventExporter : IEventExporter
    {
        private readonly TimeSpan _eventVisibilityPeriod;

        private readonly ITableWrapper _table;
        private readonly IEventMessageExporter _exporter;

        private readonly ILogger<EventExporter> _logger;

        public EventExporter(
            ITableWrapper table,
            IEventMessageExporter exporter,
            StatusAggregatorConfiguration configuration,
            ILogger<EventExporter> logger)
        {
            _table = table;
            _exporter = exporter;
            _eventVisibilityPeriod = TimeSpan.FromDays(configuration?.EventVisibilityPeriodDays ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger;
        }

        public IEnumerable<Event> Export()
        {
            return _table
                .CreateQuery<EventEntity>()
                .Where(e => (e.IsActive || (e.EndTime >= DateTime.UtcNow - _eventVisibilityPeriod)))
                .ToList()
                .SelectMany(_exporter.Export)
                .Where(e => e.Messages != null && e.Messages.Any())
                .ToList();
        }
    }
}
