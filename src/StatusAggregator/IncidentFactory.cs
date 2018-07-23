using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Incidents.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentFactory : IIncidentFactory
    {
        private readonly ITableWrapper _table;

        private readonly IEventUpdater _eventUpdater;

        public IncidentFactory(ITableWrapper table, IEventUpdater eventUpdater)
        {
            _table = table;
            _eventUpdater = eventUpdater;
        }

        public async Task<IncidentEntity> CreateIncident(ParsedIncident parsedIncident)
        {
            Console.WriteLine($"Attempting to save {parsedIncident.Id}");
            var incidentEntity = new IncidentEntity(
                parsedIncident.Id, 
                parsedIncident.AffectedComponentPath, 
                parsedIncident.AffectedComponentStatus, 
                parsedIncident.CreationTime, 
                parsedIncident.MitigationTime);

            // Find an event to attach this incident to
            var possibleEvents = _table
                .CreateQuery<EventEntity>()
                .Where(e =>
                    e.PartitionKey == EventEntity.DefaultPartitionKey &&
                    // The incident and the event must affect the same component
                    e.AffectedComponentPath == parsedIncident.AffectedComponentPath &&
                    // The event must begin before or at the same time as the incident
                    e.StartTime <= parsedIncident.CreationTime &&
                    // The event must be active or the event must end after this incident begins
                    (e.IsActive || (e.EndTime >= parsedIncident.CreationTime)))
                .ToList();

            Console.WriteLine($"Found {possibleEvents.Count()} possible events to link {parsedIncident.Id} to");

            foreach (var possibleEvent in possibleEvents)
            {
                if (!_table.GetIncidentsLinkedToEvent(possibleEvent).ToList().Any())
                {
                    Console.WriteLine($"Cannot link {parsedIncident.Id} to {possibleEvent.RowKey} because it is not linked to any incidents");
                    continue;
                }

                if (await _eventUpdater.UpdateEvent(possibleEvent, parsedIncident.CreationTime))
                {
                    Console.WriteLine($"Cannot link {parsedIncident.Id} to {possibleEvent.RowKey} because its incidents are inactive and too old");
                    continue;
                }

                Console.WriteLine($"Linking {parsedIncident.Id} to {possibleEvent.RowKey}");
                if ((int)parsedIncident.AffectedComponentStatus > possibleEvent.AffectedComponentStatus)
                {
                    Console.WriteLine($"{parsedIncident.Id} is a more severe than {possibleEvent.RowKey}, upgrading severity of event");
                    possibleEvent.AffectedComponentStatus = (int)parsedIncident.AffectedComponentStatus;
                    await _table.InsertOrReplaceAsync(possibleEvent);
                }

                incidentEntity.EventRowKey = possibleEvents.First().RowKey;
                break;
            }

            if (string.IsNullOrEmpty(incidentEntity.EventRowKey))
            {
                var eventEntity = new EventEntity(incidentEntity);
                Console.WriteLine($"Could not find existing event to attach {parsedIncident.Id} to, creating new event {eventEntity.RowKey}");
                await _table.InsertOrReplaceAsync(eventEntity);
            }

            await _table.InsertOrReplaceAsync(incidentEntity);
            return incidentEntity;
        }
    }
}
