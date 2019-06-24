// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Octokit;


namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchApiRequester : IGitHubSearchApiRequester
    {
        public async Task<GitHubSearchApiResponse> GetResponse(IGitHubClient client, SearchRepositoriesRequest request)
        {
            var apiResponse = await client.Connection.Get<SearchRepositoryResult>(ApiUrls.SearchRepositories(), request.Parameters, null);
            return new GitHubSearchApiResponse(
                         apiResponse.Body,
                         DateTime.ParseExact(apiResponse.HttpResponse.Headers["Date"], "ddd',' dd MMM yyyy HH:mm:ss 'GMT'", System.Globalization.CultureInfo.InvariantCulture).ToLocalTime(),
                         DateTimeOffset.FromUnixTimeSeconds(long.Parse(apiResponse.HttpResponse.Headers["X-RateLimit-Reset"])).ToLocalTime());
        }
    }
}
