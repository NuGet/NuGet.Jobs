using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace StatusAggregator.Incidents
{
    public class IncidentCollector : IIncidentCollector
    {

        private const string IncidentApiIncidentsEndpoint = "incidents";
        private static readonly string IncidentApiIndividualIncidentQueryFormatString = $"{IncidentApiIncidentsEndpoint}({{0}})";
        private static readonly string IncidentApiIncidentListQueryFormatString = $"{IncidentApiIncidentsEndpoint}?{{0}}";

        private static readonly JsonSerializerSettings _incidentApiJsonSerializerSettings =
            new JsonSerializerSettings() { DateTimeZoneHandling = DateTimeZoneHandling.Utc };

        public IncidentCollector(
            Uri incidentApiBaseUri, 
            X509Certificate2 incidentApiCertificate, 
            string incidentApiRoutingId)
        {
            _incidentApiBaseUri = incidentApiBaseUri;
            _incidentApiCertificate = incidentApiCertificate;
            _incidentApiRoutingId = incidentApiRoutingId;
        }

        public Task<Incident> GetIncident(string id)
        {
            return GetIncidentApiResponse<Incident>(GetIncidentApiGetIncidentQuery(id));
        }

        public async Task<IEnumerable<Incident>> GetRecentIncidents(DateTime since)
        {
            var incidents = new List<Incident>();

            string query = since == DateTime.MinValue
                ? GetIncidentApiIncidentListAllIncidentsQuery()
                : GetIncidentApiIncidentListRecentIncidentsQuery(since);
            var nextLink = GetIncidentApiUri(GetIncidentApiIncidentList(query));
            do
            {
                var incidentList = await GetIncidentApiResponse<IncidentList>(nextLink);
                foreach (var incident in incidentList.Incidents)
                {
                    // The incident API trims the milliseconds from any filter.
                    // Therefore, a query asking for incidents newer than '2018-06-29T00:00:00.5Z' will return an incident from '2018-06-29T00:00:00.25Z'
                    // We must perform a check on the CreateDate ourselves to verify that no old incidents are returned.
                    if (incident.CreateDate <= since)
                    {
                        continue;
                    }

                    incidents.Add(incident);
                }

                nextLink = incidentList.NextLink;
            } while (nextLink != null);

            return incidents;
        }

        private readonly Uri _incidentApiBaseUri;
        private readonly X509Certificate2 _incidentApiCertificate;
        private readonly string _incidentApiRoutingId;

        private string GetIncidentApiIncidentList(string oDataQueryParameters)
        {
            return string.Format(IncidentApiIncidentListQueryFormatString, oDataQueryParameters);
        }

        private string GetIncidentApiGetIncidentQuery(string id)
        {
            return string.Format(IncidentApiIndividualIncidentQueryFormatString, id);
        }

        private string GetIncidentApiIncidentListAllIncidentsQuery()
        {
            return $"$filter=RoutingId eq '{_incidentApiRoutingId}'";
        }

        private string GetIncidentApiIncidentListRecentIncidentsQuery(DateTime cursor)
        {
            return $"$filter=RoutingId eq '{_incidentApiRoutingId}' and CreateDate gt datetime'{cursor.ToString("o")}'";
        }

        private Uri GetIncidentApiUri(string query)
        {
            return new Uri(_incidentApiBaseUri, query);
        }

        private Task<T> GetIncidentApiResponse<T>(string query)
        {
            return GetIncidentApiResponse<T>(GetIncidentApiUri(query));
        }

        private async Task<T> GetIncidentApiResponse<T>(Uri uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.ClientCertificates.Add(_incidentApiCertificate);
            var response = await request.GetResponseAsync();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var content = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(content, _incidentApiJsonSerializerSettings);
            }
        }
    }
}
