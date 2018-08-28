// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupUpdater : IIncidentGroupUpdater
    {
        public readonly TimeSpan _groupEndDelay;

        private readonly ITableWrapper _table;
        private readonly IMessageUpdater _messageUpdater;

        private readonly ILogger<IncidentGroupUpdater> _logger;

        public IncidentGroupUpdater(
            ITableWrapper table, 
            IMessageUpdater messageUpdater, 
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentGroupUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _messageUpdater = messageUpdater ?? throw new ArgumentNullException(nameof(messageUpdater));
            _groupEndDelay = TimeSpan.FromMinutes(configuration?.GroupEndDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateActiveIncidentGroups(DateTime cursor)
        {
            using (_logger.Scope("Updating active groups."))
            {
                var activeGroups = _table.GetActiveGroups().ToList();
                _logger.LogInformation("Updating {ActiveGroupsCount} active groups.", activeGroups.Count());
                foreach (var activeGroup in activeGroups)
                {
                    await UpdateIncidentGroup(activeGroup, cursor);
                }
            }
        }

        public async Task<bool> UpdateIncidentGroup(IncidentGroupEntity groupEntity, DateTime cursor)
        {
            groupEntity = groupEntity ?? throw new ArgumentNullException(nameof(groupEntity));

            using (_logger.Scope("Updating group '{GroupRowKey}' given cursor {Cursor}.", groupEntity.RowKey, cursor))
            {
                if (!groupEntity.IsActive)
                {
                    _logger.LogInformation("Group is inactive, cannot update.");
                    return false;
                }

                var incidentsLinkedToGroupQuery = _table.GetIncidentsLinkedToGroup(groupEntity);

                var incidentsLinkedToGroup = incidentsLinkedToGroupQuery.ToList();
                if (!incidentsLinkedToGroup.Any())
                {
                    _logger.LogInformation("Group has no linked incidents and must have been created manually, cannot update.");
                    return false;
                }

                var shouldDeactivate = !incidentsLinkedToGroupQuery
                        .Where(i => i.IsActive || i.MitigationTime > cursor - _groupEndDelay)
                        .ToList()
                        .Any();

                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating group because its incidents are inactive and too old.");
                    var mitigationTime = incidentsLinkedToGroup
                        .Max(i => i.MitigationTime ?? DateTime.MinValue);
                    groupEntity.EndTime = mitigationTime;
                    
                    await _table.InsertOrReplaceAsync(groupEntity);
                }
                else
                {
                    _logger.LogInformation("Group has active or recent incidents so it will not be deactivated.");
                }

                return shouldDeactivate;
            }
        }
    }
}
