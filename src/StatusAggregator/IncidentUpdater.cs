using NuGet.Services.Status.Table;
using StatusAggregator.Incidents;
using StatusAggregator.Incidents.Parse;
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
        private readonly IIncidentCollector _incidentCollector;
        private readonly IIncidentFactory _incidentFactory;

        public IncidentUpdater(
            ITableWrapper table,
            IEventUpdater eventUpdater,
            IIncidentCollector incidentCollector,
            IAggregateIncidentParser aggregateIncidentParser,
            IIncidentFactory incidentFactory)
        {
            _table = table;
            _eventUpdater = eventUpdater;
            _incidentCollector = incidentCollector;
            _aggregateIncidentParser = aggregateIncidentParser;
            _incidentFactory = incidentFactory;
        }

        public async Task RefreshExistingIncidents()
        {
            var activeIncidentEntities = _table
                .CreateQuery<IncidentEntity>()
                .Where(i => i.PartitionKey == IncidentEntity.DefaultPartitionKey && i.IsActive);

            foreach (var activeIncidentEntity in activeIncidentEntities)
            {
                var activeIncident = await _incidentCollector.GetIncident(activeIncidentEntity.IncidentApiId);
                activeIncidentEntity.MitigationTime = activeIncident.MitigationData?.Date;
                await _table.InsertOrReplaceAsync(activeIncidentEntity);
            }
        }

        public async Task<DateTime?> FetchNewIncidents(DateTime cursor)
        {
            var incidents = await _incidentCollector.GetRecentIncidents(cursor);

            var parsedIncidents = incidents.SelectMany(i => _aggregateIncidentParser.ParseIncident(i));
            foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.CreationTime))
            {
                await _incidentFactory.CreateIncident(parsedIncident);
            }

            return incidents.Any() ? incidents.Max(i => i.CreateDate) : (DateTime?)null;
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
