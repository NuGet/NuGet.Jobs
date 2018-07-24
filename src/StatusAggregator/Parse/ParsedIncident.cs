using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System;

namespace StatusAggregator.Parse
{
    public class ParsedIncident
    {
        public ParsedIncident(
            Incident incident, 
            string affectedComponentPath,
            ComponentStatus affectedComponentStatus)
        {
            Id = incident.Id;
            CreationTime = incident.Source.CreateDate;
            MitigationTime = incident.MitigationData?.Date;
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = affectedComponentStatus;
        }

        public string Id { get; set; }
        public string AffectedComponentPath { get; set; }
        public ComponentStatus AffectedComponentStatus { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? MitigationTime { get; set; }
    }
}
