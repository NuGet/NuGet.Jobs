﻿using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class ManualStatusChangeUpdater : IManualStatusChangeUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IManualStatusChangeHandler _handler;
        private readonly ILogger<ManualStatusChangeUpdater> _logger;

        public ManualStatusChangeUpdater(
            ITableWrapper table,
            IManualStatusChangeHandler handler,
            ILogger<ManualStatusChangeUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DateTime?> ProcessNewManualChanges(DateTime cursor)
        {
            using (_logger.Scope("Processing manual status changes."))
            {
                var manualChangesQuery = _table
                    .CreateQuery<ManualStatusChangeEntity>()
                    .Where(c => c.PartitionKey == ManualStatusChangeEntity.DefaultPartitionKey);

                // Table storage throws on queries with DateTime values that are too low.
                // If we are fetching manual changes for the first time, don't filter on the timestamp.
                if (cursor > DateTime.MinValue)
                {
                    manualChangesQuery = manualChangesQuery.Where(c => c.ChangeTimestamp > cursor);
                }
                
                var manualChanges = manualChangesQuery.ToList();

                _logger.LogInformation("Processing {ManualChangesCount} manual status changes.", manualChanges.Count());
                foreach (var manualChange in manualChanges)
                {
                    await _handler.Handle(manualChange);
                }

                return manualChanges.Any() ? manualChanges.Max(c => c.ChangeTimestamp) : (DateTime?)null;
            }
        }
    }
}
