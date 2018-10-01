// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class IncidentUpdater : IIncidentUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IAggregateIncidentParser _aggregateIncidentParser;
        private readonly IIncidentApiClient _incidentApiClient;
        private readonly IComponentAffectingEntityFactory<IncidentEntity> _incidentFactory;
        private readonly ILogger<IncidentUpdater> _logger;

        private readonly string _incidentApiTeamId;

        public IncidentUpdater(
            ITableWrapper table,
            IIncidentApiClient incidentApiClient,
            IAggregateIncidentParser aggregateIncidentParser,
            IComponentAffectingEntityFactory<IncidentEntity> incidentFactory,
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _incidentApiClient = incidentApiClient ?? throw new ArgumentNullException(nameof(incidentApiClient));
            _aggregateIncidentParser = aggregateIncidentParser ?? throw new ArgumentNullException(nameof(aggregateIncidentParser));
            _incidentFactory = incidentFactory ?? throw new ArgumentNullException(nameof(incidentFactory));
            _incidentApiTeamId = configuration?.TeamId ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DateTime?> FetchNewIncidents(DateTime cursor)
        {
            using (_logger.Scope("Fetching all new incidents since {Cursor}.", cursor))
            {
                var incidents = (await _incidentApiClient.GetIncidents(GetRecentIncidentsQuery(cursor)))
                    // The incident API trims the milliseconds from any filter.
                    // Therefore, a query asking for incidents newer than '2018-06-29T00:00:00.5Z' will return an incident from '2018-06-29T00:00:00.25Z'
                    // We must perform a check on the CreateDate ourselves to verify that no old incidents are returned.
                    .Where(i => i.CreateDate > cursor)
                    .ToList();

                var parsedIncidents = incidents
                    .SelectMany(i => _aggregateIncidentParser.ParseIncident(i))
                    .ToList();
                foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.StartTime))
                {
                    await _incidentFactory.Create(parsedIncident);
                }

                return incidents.Any() ? incidents.Max(i => i.CreateDate) : (DateTime?)null;
            }
        }
        
        private string GetRecentIncidentsQuery(DateTime cursor)
        {
            var query = $"$filter=OwningTeamId eq '{_incidentApiTeamId}'";

            if (cursor != DateTime.MinValue)
            {
                query += $" and CreateDate gt datetime'{cursor.ToString("o")}'";
            }

            return query;
        }
    }
}
