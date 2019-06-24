// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchWrapper : IGitHubSearchWrapper
    {
        private readonly IGitHubClient _client;

        public GitHubSearchWrapper(IGitHubClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public int? GetRemainingRequestCount()
        {
            var apiInfo = _client.GetLastApiInfo();
            return apiInfo == null ? (int?)null : apiInfo.RateLimit.Remaining;
        }

        public async Task<GitHubSearchApiResponse> GetResponse(SearchRepositoriesRequest request)
        {
            var apiResponse = await _client.Connection.Get<SearchRepositoryResult>(ApiUrls.SearchRepositories(), request.Parameters, null);
            return new GitHubSearchApiResponse(
                         apiResponse.Body,
                         DateTime.ParseExact(apiResponse.HttpResponse.Headers["Date"], "ddd',' dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture).ToLocalTime(),
                         DateTimeOffset.FromUnixTimeSeconds(long.Parse(apiResponse.HttpResponse.Headers["X-RateLimit-Reset"])).ToLocalTime());
        }
    }
}
