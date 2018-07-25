using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
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

        private readonly IEventUpdater _eventUpdater;

        private readonly IAggregateIncidentParser _aggregateIncidentParser;
        private readonly IIncidentApiClient _incidentApiClient;
        private readonly IIncidentFactory _incidentFactory;

        private readonly string _incidentApiTeamId;

        private readonly ILogger<IncidentUpdater> _logger;

        public IncidentUpdater(
            ITableWrapper table,
            IEventUpdater eventUpdater,
            IIncidentApiClient incidentApiClient,
            IAggregateIncidentParser aggregateIncidentParser,
            IIncidentFactory incidentFactory,
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentUpdater> logger)
        {
            _table = table;
            _eventUpdater = eventUpdater;
            _incidentApiClient = incidentApiClient;
            _aggregateIncidentParser = aggregateIncidentParser;
            _incidentFactory = incidentFactory;
            _incidentApiTeamId = configuration.TeamId;
            _logger = logger;
        }

        public async Task RefreshExistingIncidents()
        {
            using (_logger.Scope(
                "Beginning to refresh existing incidents.",
                "Finished refreshing existing incidents.",
                "Refreshing existing incidents."))
            {
                var activeIncidentEntities = _table
                    .CreateQuery<IncidentEntity>()
                    .Where(i => i.PartitionKey == IncidentEntity.DefaultPartitionKey && i.IsActive);

                foreach (var activeIncidentEntity in activeIncidentEntities)
                {
                    using (_logger.Scope(
                        "Beginning to refresh incident.",
                        "Finished refreshing incident.",
                        "Refreshing incident '{IncidentRowKey}'.", activeIncidentEntity.RowKey))
                    {
                        var activeIncident = await _incidentApiClient.GetIncident(activeIncidentEntity.IncidentApiId);
                        activeIncidentEntity.MitigationTime = activeIncident.MitigationData?.Date;
                        _logger.LogInformation("Updated mitigation time of incident to {MitigationTime}", activeIncidentEntity.MitigationTime);
                        await _table.InsertOrReplaceAsync(activeIncidentEntity);
                    }
                }
            }
        }

        public async Task<DateTime?> FetchNewIncidents(DateTime cursor)
        {
            using (_logger.Scope(
                "Beginning to fetch new incidents.",
                "Finished fetching new incidents.",
                "Fetching all new incidents since {Cursor}.", cursor))
            {
                var incidents = (await _incidentApiClient.GetIncidents(GetRecentIncidentsQuery(cursor)))
                    // The incident API trims the milliseconds from any filter.
                    // Therefore, a query asking for incidents newer than '2018-06-29T00:00:00.5Z' will return an incident from '2018-06-29T00:00:00.25Z'
                    // We must perform a check on the CreateDate ourselves to verify that no old incidents are returned.
                    .Where(i => i.CreateDate > cursor);

                var parsedIncidents = incidents
                    .SelectMany(i => _aggregateIncidentParser.ParseIncident(i))
                    .ToList();
                foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.CreationTime))
                {
                    await _incidentFactory.CreateIncident(parsedIncident);
                }

                return incidents.Any() ? incidents.Max(i => i.CreateDate) : (DateTime?)null;
            }
        }
        
        private string GetRecentIncidentsQuery(DateTime cursor)
        {
            var cursorPart = cursor == DateTime.MinValue
                ? ""
                : $" and CreateDate gt datetime'{cursor.ToString("o")}'";

            return $"$filter=OwningTeamId eq '{_incidentApiTeamId}'{cursorPart}";
        }
    }
}
