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
        private readonly IIncidentApiClient _incidentClient;
        private readonly IIncidentFactory _incidentFactory;

        private readonly string _incidentTeamId;

        public IncidentUpdater(
            ITableWrapper table,
            IEventUpdater eventUpdater,
            IIncidentApiClient incidentClient,
            IAggregateIncidentParser aggregateIncidentParser,
            IIncidentFactory incidentFactory,
            string incidentTeamId)
        {
            _table = table;
            _eventUpdater = eventUpdater;
            _incidentClient = incidentClient;
            _aggregateIncidentParser = aggregateIncidentParser;
            _incidentFactory = incidentFactory;
            _incidentTeamId = incidentTeamId;
        }

        public async Task RefreshExistingIncidents()
        {
            var activeIncidentEntities = _table
                .CreateQuery<IncidentEntity>()
                .Where(i => i.PartitionKey == IncidentEntity.DefaultPartitionKey && i.IsActive);

            foreach (var activeIncidentEntity in activeIncidentEntities)
            {
                var activeIncident = await _incidentClient.GetIncident(activeIncidentEntity.IncidentApiId);
                activeIncidentEntity.MitigationTime = activeIncident.MitigationData?.Date;
                await _table.InsertOrReplaceAsync(activeIncidentEntity);
            }
        }

        public async Task<DateTime?> FetchNewIncidents(DateTime cursor)
        {
            var incidents = (await _incidentClient.GetIncidents(GetRecentIncidentsQuery(cursor)))
                // The incident API trims the milliseconds from any filter.
                // Therefore, a query asking for incidents newer than '2018-06-29T00:00:00.5Z' will return an incident from '2018-06-29T00:00:00.25Z'
                // We must perform a check on the CreateDate ourselves to verify that no old incidents are returned.
                .Where(i => i.CreateDate > cursor);

            var parsedIncidents = incidents.SelectMany(i => _aggregateIncidentParser.ParseIncident(i));
            foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.CreationTime))
            {
                await _incidentFactory.CreateIncident(parsedIncident);
            }

            return incidents.Any() ? incidents.Max(i => i.CreateDate) : (DateTime?)null;
        }
        
        private string GetRecentIncidentsQuery(DateTime cursor)
        {
            var cursorPart = cursor == DateTime.MinValue
                ? ""
                : $" and CreateDate gt datetime'{cursor.ToString("o")}'";

            return $"$filter=OwningTeamId eq '{_incidentTeamId}'{cursorPart}";
        }

        public async Task UpdateActiveEvents(DateTime cursor)
        {
            var eventsToCheckClosure = _table.GetActiveEvents();
            foreach (var eventToCheckClosure in eventsToCheckClosure)
            {
                await _eventUpdater.UpdateEvent(eventToCheckClosure, cursor);
            }
        }
    }
}
