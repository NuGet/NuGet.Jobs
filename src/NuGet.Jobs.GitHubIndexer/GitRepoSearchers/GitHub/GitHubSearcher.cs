// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcher : IGitRepoSearcher
    {
        private readonly ILogger<GitHubSearcher> _logger;
        private readonly IOptionsSnapshot<GitHubSearcherConfiguration> _configuration;
        private readonly IGitHubSearchWrapper _searchApiRequester;

        private DateTimeOffset _throttleResetTime;

        public GitHubSearcher(
            IGitHubSearchWrapper searchApiRequester,
            ILogger<GitHubSearcher> logger,
            IOptionsSnapshot<GitHubSearcherConfiguration> configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _searchApiRequester = searchApiRequester ?? throw new ArgumentNullException(nameof(searchApiRequester));
        }

        private int _minStars => _configuration.Value.MinStars;
        private int _resultsPerPage => _configuration.Value.ResultsPerPage;
        private int _maxGithubResultPerQuery => _configuration.Value.MaxGitHubResultsPerQuery;

        private async Task CheckThrottle()
        {
            if (_searchApiRequester.GetRemainingRequestCount() == 0)
            {
                var sleepTime = _throttleResetTime - DateTimeOffset.Now;
                _throttleResetTime = DateTimeOffset.Now;
                if (sleepTime.TotalSeconds > 0)
                {
                    _logger.LogInformation($"Waiting {sleepTime.TotalSeconds} seconds to cooldown.");
                    await Task.Delay(sleepTime);
                }

                _logger.LogInformation("Resuming search.");
            }
        }

        private async Task<SearchRepositoryResult> SearchRepo(SearchRepositoriesRequest request)
        {
            _logger.LogInformation("Requesting page {Page} for stars {Stars}", request.Page, request.Stars);

            bool? error = null;
            GitHubSearchApiResponse response = null;
            while (!error.HasValue || error.Value)
            {
                try
                {
                    response = await _searchApiRequester.GetResponse(request);
                    error = false;
                }
                catch (RateLimitExceededException)
                {
                    _logger.LogError("Exceeded GitHub RateLimit! Waiting 5 seconds before retrying.");
                    await Task.Delay(5_000);
                }
            }

            if (_throttleResetTime < DateTimeOffset.Now)
            {
                var timeToWait = response.ThrottleResetTime - response.Date;
                _throttleResetTime = DateTimeOffset.Now + timeToWait;
            }

            return response.Result;
        }

        private async Task<List<Repository>> GetResultsFromGitHub()
        {
            _throttleResetTime = DateTimeOffset.Now;
            var upperStarBound = int.MaxValue;
            var resultList = new List<Repository>();
            var lastPage = Math.Ceiling(_maxGithubResultPerQuery / (double)_resultsPerPage);

            while (upperStarBound >= _minStars)
            {
                var page = 0;
                while (page < lastPage)
                {
                    await CheckThrottle();

                    var request = new SearchRepositoriesRequest
                    {
                        Stars = new Range(_minStars, upperStarBound),
                        Language = Language.CSharp,
                        SortField = RepoSearchSort.Stars,
                        Order = SortDirection.Descending,
                        PerPage = _resultsPerPage,
                        Page = page + 1
                    };

                    var response = await SearchRepo(request);

                    if (response.Items == null || !response.Items.Any())
                    {
                        _logger.LogWarning($"Search request didn't return any item. Page: {request.Page} {GetConfigInfo()}");
                        return resultList;
                    }

                    // TODO: Block unwanted repos (https://github.com/NuGet/NuGetGallery/issues/7298)
                    resultList.AddRange(response.Items);
                    page++;

                    if (page == lastPage && response.Items.First().StargazersCount == response.Items.Last().StargazersCount)
                    {
                        // This may result in missing data since more the entire result set produced by a query has the same star count, we can't produce
                        // an "all the repos with the same star count but that are not these ones" query
                        _logger.LogWarning("Last page results have the same star count! This may result in missing data. StarCount: {Stars} {ConfigInfo}", response.Items.First().StargazersCount, GetConfigInfo());
                        return resultList;
                    }
                }

                upperStarBound = resultList.Last().StargazersCount;
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
            var result = await GetResultsFromGitHub();
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
                .OrderByDescending(x => x.Stars)
                .ToList();
        }

        private string GetConfigInfo()
        {
            return $"MinStars: {_minStars}\n" +
               $"ResultsPerPage: {_resultsPerPage}\n" +
               $"MaxGithubResultPerQuery: {_maxGithubResultPerQuery}\n";
        }
    }
}
