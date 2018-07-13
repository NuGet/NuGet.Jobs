using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status;

namespace StatusAggregator.Table
{
    public class EventEntity : TableEntity
    {
        public const string DefaultPartitionKey = "events";

        public EventEntity()
        {
        }

        public EventEntity(IncidentEntity incidentEntity)
            : base(DefaultPartitionKey, GetRowKey(incidentEntity))
        {
            AffectedComponentPath = incidentEntity.AffectedComponentPath;
            AffectedComponentStatus = incidentEntity.AffectedComponentStatus;
            StartTime = incidentEntity.CreationTime;
            incidentEntity.EventRowKey = RowKey;
        }

        public string AffectedComponentPath { get; set; }
        public ComponentStatus AffectedComponentStatus { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive
        {
            get { return EndTime == null; }
            set { }
        }

        public Event AsEvent(IEnumerable<Message> messages)
        {
            return new Event(AffectedComponentPath, AffectedComponentStatus, StartTime, EndTime, messages);
        }

        private static string GetRowKey(IncidentEntity incidentEntity)
        {
            return $"{TableUtility.ToRowKeySafeComponentPath(incidentEntity.AffectedComponentPath)}_{incidentEntity.CreationTime.ToString("o")}";
        }
    }
}
