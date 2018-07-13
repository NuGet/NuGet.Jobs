using System;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status;
using StatusAggregator.Incidents.Parse;

namespace StatusAggregator.Table
{
    public class IncidentEntity : TableEntity
    {
        public const string DefaultPartitionKey = "incidents";

        public IncidentEntity()
        {
        }

        public IncidentEntity(ParsedIncident parsedIncident)
            : base(DefaultPartitionKey, GetRowKey(parsedIncident))
        {
            IncidentApiId = parsedIncident.Id;
            AffectedComponentPath = parsedIncident.AffectedComponentPath;
            AffectedComponentStatus = (int)parsedIncident.AffectedComponentStatus;
            CreationTime = parsedIncident.CreationTime;
            MitigationTime = parsedIncident.MitigationTime;
        }

        public string EventRowKey { get; set; }
        public bool IsLinkedToEvent
        {
            get { return !string.IsNullOrEmpty(EventRowKey); }
            set { }
        }
        public string IncidentApiId { get; set; }
        public string AffectedComponentPath { get; set; }
        public int AffectedComponentStatus { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? MitigationTime { get; set; }
        public bool IsActive
        {
            get { return MitigationTime == null; }
            set { }
        }

        private static string GetRowKey(ParsedIncident parsedIncident)
        {
            return $"{parsedIncident.Id}_{TableUtility.ToRowKeySafeComponentPath(parsedIncident.AffectedComponentPath)}_{parsedIncident.AffectedComponentStatus}";
        }
    }
}
