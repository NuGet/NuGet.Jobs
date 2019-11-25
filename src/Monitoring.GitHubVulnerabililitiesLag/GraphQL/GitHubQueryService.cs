// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public class GitHubQueryService : IGitHubQueryService
    {
        /// <remarks>
        /// GitHub requires that every request includes a UserAgent.
        /// </remarks>
        public const string UserAgent = "NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag";

        private readonly GitHubVulnerabilitiesLagMonitorConfiguration _configuration;
        private readonly IGitHubVulnerabilitiesLagTelemetryService _telemetryService;
        private readonly ILogger<GitHubQueryService> _logger;
        private readonly HttpClient _httpClient;

        public GitHubQueryService(
            HttpClient httpClient,
            GitHubVulnerabilitiesLagMonitorConfiguration configuration,
            IGitHubVulnerabilitiesLagTelemetryService telemetryService,
            ILogger<GitHubQueryService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DateTimeOffset?> GetLatestAdvisoryUpdatedAtValueAfterCursor(
            DateTimeOffset cursorValue,
            CancellationToken cancellationToken)
        {
            var query =
                @"{
                    securityAdvisories(first: 100, orderBy: {field: UPDATED_AT, direction: DESC}, updatedSince: """ + cursorValue.ToString("O") + @""") {
                        edges {
                            node {
                                updatedAt
                                vulnerabilities(first: 1, ecosystem: NUGET, orderBy: {field: UPDATED_AT, direction: DESC}) {
                                  edges {
                                    node {
                                      updatedAt
                                    }
                                  }
                                }
                            }
                        }
                    }
                }";

            var response = await QueryAsync(query, cancellationToken);

            // Check if there are any new advisories.
            if (!response.Data.SecurityAdvisories.Edges.Any())
            {
                return null;
            }

            // Filter the result to only keep the advisories with known vulnerabilities affecting the NuGet ecosystem.
            var advisoriesWithNuGetVulnerabilities = response.Data.SecurityAdvisories.Edges.Where(e => e.Node.Vulnerabilities.Edges.Any());

            // The query already sorted the response by UPDATED_AT descending, so we can take the first element in each enumeration.
            return advisoriesWithNuGetVulnerabilities.First().Node.Vulnerabilities.Edges.First().Node.UpdatedAt;
        }

        private async Task<QueryResponse> QueryAsync(string query, CancellationToken token)
        {
            var queryJObject = new JObject
            {
                ["query"] = query
            };

            var response = await MakeWebRequestAsync(queryJObject.ToString(), token);
            return JsonConvert.DeserializeObject<QueryResponse>(response);
        }

        private async Task<string> MakeWebRequestAsync(string query, CancellationToken token)
        {
            using (var request = CreateRequest(query))
            using (var response = await _httpClient.SendAsync(request, token))
            {
                return await response.Content.ReadAsStringAsync();
            }
        }

        private HttpRequestMessage CreateRequest(string query)
        {
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _configuration.GitHubGraphQLQueryEndpoint,
                Content = new StringContent(query, Encoding.UTF8, "application/json")
            };

            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _configuration.GitHubPersonalAccessToken);
            message.Headers.UserAgent.TryParseAdd(UserAgent);
            return message;
        }
    }
}
