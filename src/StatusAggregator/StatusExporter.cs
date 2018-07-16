using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.Status;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class StatusExporter : IStatusExporter
    {
        private const string StatusBlobName = "status.json";
        private static TimeSpan EventVisibilityPeriod = TimeSpan.FromDays(7);

        private readonly CloudBlobContainer _container;
        private readonly ITableWrapper _table;

        private static readonly JsonSerializerSettings _statusBlobJsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new StatusContractResolver(),
            Converters = new List<JsonConverter>() { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        public StatusExporter(CloudBlobContainer container, ITableWrapper table)
        {
            _container = container;
            _table = table;
        }

        public async Task Export()
        {
            var rootComponent = Components.CreateNuGetServiceRootComponent();

            var recentEvents = _table
                .CreateQuery<EventEntity>()
                .Where(e =>
                    e.PartitionKey == EventEntity.DefaultPartitionKey &&
                    (e.IsActive || (e.EndTime >= DateTime.UtcNow - EventVisibilityPeriod)))
                .ToList()
                .Select(e =>
                {
                    var messages = _table.GetMessagesLinkedToEvent(e)
                        .ToList()
                        .Select(m => m.AsMessage());
                    return e.AsEvent(messages);
                })
                .Where(e => e.Messages != null && e.Messages.Any());

            foreach (var activeEvent in recentEvents.Where(e => e.EndTime == null || e.EndTime >= DateTime.UtcNow))
            {
                var currentComponent = rootComponent.GetByPath(activeEvent.AffectedComponentPath);

                if (currentComponent == null)
                {
                    continue;
                }

                currentComponent.Status = activeEvent.AffectedComponentStatus;
            }
            

            var status = new ServiceStatus(rootComponent, recentEvents);
            var statusJson = JsonConvert.SerializeObject(status, _statusBlobJsonSerializerSettings);

            var blob = _container.GetBlockBlobReference(StatusBlobName);
            await blob.UploadTextAsync(statusJson);
        }
    }
}
