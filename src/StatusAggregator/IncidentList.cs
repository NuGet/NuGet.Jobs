using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StatusAggregator
{
    public class IncidentList
    {
        [JsonProperty(PropertyName = "value")]
        public IEnumerable<Incident> Incidents { get; set; }
        
        [JsonProperty(PropertyName = "odata.nextLink")]
        public Uri NextLink { get; set; }
    }
}
