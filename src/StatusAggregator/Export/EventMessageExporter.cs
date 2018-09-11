// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Export
{
    public class EventMessageExporter : IEventMessageExporter
    {
        private readonly ITableWrapper _table;
        private readonly Func<EventEntity, IEventMessageExportIterator> _iteratorFactory;
        private readonly ILogger<EventMessageExporter> _logger;

        public EventMessageExporter(
            ITableWrapper table,
            Func<EventEntity, IEventMessageExportIterator> iteratorFactory,
            ILogger<EventMessageExporter> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _iteratorFactory = iteratorFactory ?? throw new ArgumentNullException(nameof(iteratorFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<Event> Export(EventEntity eventEntity)
        {
            using (_logger.Scope("Exporting event {EventRowKey}.", eventEntity.RowKey))
            {
                var messages = _table.GetLinkedEntities<MessageEntity, EventEntity>(eventEntity)
                    .ToList()
                    // Don't show empty messages.
                    .Where(m => !string.IsNullOrEmpty(m.Contents))
                    .ToList();

                _logger.LogInformation("Event has {MessageCount} messages. Iterating through each message.", messages.Count);
                var iterator = _iteratorFactory(eventEntity);
                foreach (var message in messages.OrderBy(m => m.Time))
                {
                    iterator.Process(message);
                }

                return iterator.Export();
            }
        }
    }
}
