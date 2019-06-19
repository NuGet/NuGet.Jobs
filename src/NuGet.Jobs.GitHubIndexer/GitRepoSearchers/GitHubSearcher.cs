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
        private readonly IGitHubClient _client;
        private readonly ILogger<GitHubSearcher> _logger;
        private readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
        private readonly IOptionsSnapshot<GitHubSearcherConfiguration> _configuration;

        public GitHubSearcher(
            IGitHubClient client,
            ILogger<GitHubSearcher> logger,
            IOptionsSnapshot<GitHubSearcherConfiguration> configuration)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _logger.LogInformation(
                $"GitHubSearcher created with params:\n" + GetConfigInfo());
        }

        public int MinStars => _configuration.Value.MinStars;
        public int ResultsPerPage => _configuration.Value.ResultsPerPage;
        public int MaxGithubResultPerQuery => _configuration.Value.MaxGithubResultPerQuery;

        private async Task CheckThrottle()
        {
            if (_client.GetLastApiInfo() != null && _client.GetLastApiInfo().RateLimit.Remaining == 0)
            {
                _logger.LogInformation("Waiting a minute to cooldown.");
                await Task.Delay(OneMinute);
                _logger.LogInformation("Resuming search.");
            }
        }

        private async Task<List<Repository>> GetResultsFromGitHub()
        {
            var upperStarBound = int.MaxValue;
            var resultList = new List<Repository>();
            var lastPage = Math.Ceiling(MaxGithubResultPerQuery / (double)ResultsPerPage);
            while (upperStarBound >= MinStars)
            {
                var page = 0;
                while (page < lastPage)
                {
                    await CheckThrottle();
                    var request = new SearchRepositoriesRequest
                    {
                        Stars = new Range(MinStars, upperStarBound),
                        Language = Language.CSharp,
                        SortField = RepoSearchSort.Stars,
                        Order = SortDirection.Descending,
                        PerPage = ResultsPerPage,
                        Page = page + 1
                    };

                    var response = await _client.Search.SearchRepo(request);
                    if (response.Items == null || !response.Items.Any())
                    {
                        return resultList;
                    }

                    // TODO: Block unwanted repos
                    resultList.AddRange(response.Items);
                    page++;

                    if (page >= lastPage && response.Items.First().StargazersCount == response.Items.Last().StargazersCount)
                    {
                        _logger.LogWarning($"Last page results have the same star count! StarCount: {response.Items.First().StargazersCount}\n{GetConfigInfo()}"); // TODO
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
            return $"MinStars: {MinStars}\n" +
               $"ResultsPerPage: {ResultsPerPage}\n" +
               $"MaxGithubResultPerQuery: {MaxGithubResultPerQuery}\n";
        }
    }
}
