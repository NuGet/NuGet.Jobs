// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentFactory : IIncidentFactory
    {
        private readonly ITableWrapper _table;
        private readonly IIncidentGroupUpdater _incidentGroupUpdater;

        private readonly ILogger<IncidentFactory> _logger;

        public IncidentFactory(
            ITableWrapper table, 
            IIncidentGroupUpdater incidentGroupUpdater, 
            ILogger<IncidentFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _incidentGroupUpdater = incidentGroupUpdater ?? throw new ArgumentNullException(nameof(incidentGroupUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IncidentEntity> CreateIncident(ParsedIncident parsedIncident)
        {
            var incidentEntity = new IncidentEntity(
                parsedIncident.Id,
                parsedIncident.AffectedComponentPath,
                parsedIncident.AffectedComponentStatus,
                parsedIncident.CreationTime,
                parsedIncident.MitigationTime);

            using (_logger.Scope("Creating incident '{IncidentRowKey}'.", incidentEntity.RowKey))
            {
                // Find a group to attach this incident to
                var possibleIncidentGroups = _table
                    .CreateQuery<IncidentGroupEntity>()
                    .Where(e =>
                        e.PartitionKey == IncidentGroupEntity.DefaultPartitionKey &&
                        // The incident and the group must affect the same component
                        e.AffectedComponentPath == incidentEntity.AffectedComponentPath &&
                        // The group must begin before or at the same time as the incident
                        e.StartTime <= incidentEntity.CreationTime &&
                        // The group must be active or the group must end after this incident begins
                        (e.IsActive || (e.EndTime >= incidentEntity.CreationTime)))
                    .ToList();

                _logger.LogInformation("Found {GroupCount} possible groups to link incident to.", possibleIncidentGroups.Count());
                IncidentGroupEntity groupToLinkTo = null;
                foreach (var possibleGroupToLinkTo in possibleIncidentGroups)
                {
                    if (!_table.GetIncidentsLinkedToGroup(possibleGroupToLinkTo).ToList().Any())
                    {
                        _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it is not linked to any incidents.", possibleGroupToLinkTo.RowKey);
                        continue;
                    }

                    if (await _incidentGroupUpdater.UpdateIncidentGroup(possibleGroupToLinkTo, incidentEntity.CreationTime))
                    {
                        _logger.LogInformation("Cannot link incident to group '{GroupRowKey}' because it has been deactivated.", possibleGroupToLinkTo.RowKey);
                        continue;
                    }

                    _logger.LogInformation("Linking incident to group '{GroupRowKey}'.", possibleGroupToLinkTo.RowKey);
                    groupToLinkTo = possibleGroupToLinkTo;
                    break;
                }

                if (groupToLinkTo == null)
                {
                    groupToLinkTo = new IncidentGroupEntity(incidentEntity);
                    _logger.LogInformation("Could not find existing group to link to, creating new group '{GroupRowKey}' to link incident to.", groupToLinkTo.RowKey);
                    await _table.InsertOrReplaceAsync(groupToLinkTo);
                }

                incidentEntity.IncidentGroupRowKey = groupToLinkTo.RowKey;
                await _table.InsertOrReplaceAsync(incidentEntity);

                if ((int)parsedIncident.AffectedComponentStatus > groupToLinkTo.AffectedComponentStatus)
                {
                    _logger.LogInformation("Increasing severity of group '{GroupRowKey}' because newly linked incident is more severe than the group.", groupToLinkTo.RowKey);
                    groupToLinkTo.AffectedComponentStatus = (int)parsedIncident.AffectedComponentStatus;
                    await _table.InsertOrReplaceAsync(groupToLinkTo);
                }

                return incidentEntity;
            }
        }
    }
}
