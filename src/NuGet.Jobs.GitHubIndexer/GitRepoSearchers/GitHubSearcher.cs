// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcher : IGitRepoSearcher
    {
        public const int MinStars = 100;
        public const int ResultsPerPage = 100; // Maximum is 100 :(
        public const int MaxGithubResultPerQuery = 1000; // The maximum number of results a query can return (1000 as of 6/18/2019)
        private readonly IGitHubClient _client;
        private readonly ILogger<GitHubSearcher> _logger;
        private readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

        public GitHubSearcher(IGitHubClient client, ILogger<GitHubSearcher> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task<List<Repository>> GetResultsForPage(int currPage, int totalCount, int? maxStarCount = null, string lastRecordName = null)
        {
            if (_client.GetLastApiInfo() != null && _client.GetLastApiInfo().RateLimit.Remaining == 0)
            {
                _logger.LogInformation("Waiting a minute to cooldown.");
                await Task.Delay(OneMinute);
                _logger.LogInformation("Resuming search.");
            }

            var request = new SearchRepositoriesRequest
            {
                Stars = maxStarCount.HasValue ? Range.GreaterThan(MinStars) : new Range(MinStars, maxStarCount.Value),
                Language = Language.CSharp,
                SortField = RepoSearchSort.Stars,
                Order = SortDirection.Descending,
                PerPage = ResultsPerPage,
                Page = currPage
            };

            var resultList = new List<Repository>();

            var response = await _client.Search.SearchRepo(request);
            if (response.Items == null || !response.Items.Any())
            {
                return resultList;
            }

            var toAdd =
                lastRecordName == null ?
                    response.Items :
                    response.Items.Where(repo => repo.FullName != lastRecordName);
            resultList.AddRange(toAdd);

            // Since there can only be $RESULTS_PER_PAGE$ results per page, if the count is 100, it means we should query the next page
            if (response.Items.Count == ResultsPerPage)
            {
                ++currPage;

                if (currPage <= Math.Ceiling(MaxGithubResultPerQuery / (double)ResultsPerPage))
                {
                    resultList.AddRange(await GetResultsForPage(currPage, resultList.Count + totalCount, maxStarCount));
                }
                else
                {
                    // Since we need to grab more than 1000 results, let's pick up where the $currLast$ repository is and build a new query from there
                    // This will make us count from the $recursivePage$ parameter and not the currPage anymore
                    resultList.AddRange(await GetResultsForPage(1, resultList.Count + totalCount, response.Items[response.Items.Count - 1].StargazersCount, response.Items[response.Items.Count - 1].FullName));
                }
            }

            return resultList;
        }

        /// <summary>
        /// Searches for all the C# repos that have more than 100 stars on GitHub, orders them in Descending order and returns them.
        /// </summary>
        /// <returns>List of C# repos on GitHub that have more than 100 stars</returns>
        public async Task<IReadOnlyList<RepositoryInformation>> GetPopularRepositories()
        {
            _logger.LogInformation("Starting search on GitHub...");
            var result = await GetResultsForPage(1, 0);
            return result
                .GroupBy(x => x.FullName) // Used to remove duplicate repos (since the GH Search API may return a result that we already had in memory)
                .Select(
                group =>
                {
                    var repo = group.First();
                    return new RepositoryInformation(
                        $"{repo.Owner.Login}/{repo.Name}",
                        repo.HtmlUrl,
                        repo.StargazersCount,
                        Array.Empty<string>());
                })
                .ToList();
        }
    }
}
