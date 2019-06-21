// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Octokit;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearcherFacts
    {
        private static readonly GitHubSearcherConfiguration _configuration = new GitHubSearcherConfiguration();

        private static GitHubSearcher GetMockClient(Func<SearchRepositoriesRequest, Task<SearchRepositoryResult>> searchResultFunc = null)
        {
            var connection = new Mock<IConnection>();
            var dummyApiInfo = new ApiInfo(
                            new Dictionary<string, Uri>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            "",
                            new RateLimit(10, 10, 10));
            connection
                .Setup(c => c.GetLastApiInfo())
                .Returns(dummyApiInfo);

            var mockSearch = new Mock<ISearchClient>();
            if (searchResultFunc == null)
            {
                mockSearch
                    .Setup(s => s.SearchRepo(It.IsAny<SearchRepositoriesRequest>()))
                    .Returns(Task.FromResult(new SearchRepositoryResult()));
            }
            else
            {
                mockSearch
                    .Setup(s => s.SearchRepo(It.IsAny<SearchRepositoriesRequest>()))
                    .Returns(searchResultFunc);
            }
            var mockClient = new Mock<IGitHubClient>();
            mockClient.SetupGet(c => c.Connection).Returns(connection.Object);
            var mockApiConnection = new ApiConnection(connection.Object);
            mockClient.SetupGet(c => c.Activity).Returns(new ActivitiesClient(mockApiConnection));
            mockClient.SetupGet(c => c.Authorization).Returns(new AuthorizationsClient(mockApiConnection));
            mockClient.SetupGet(c => c.Enterprise).Returns(new EnterpriseClient(mockApiConnection));
            mockClient.SetupGet(c => c.Gist).Returns(new GistsClient(mockApiConnection));
            mockClient.SetupGet(c => c.Git).Returns(new GitDatabaseClient(mockApiConnection));
            mockClient.SetupGet(c => c.GitHubApps).Returns(new GitHubAppsClient(mockApiConnection));
            mockClient.SetupGet(c => c.Issue).Returns(new IssuesClient(mockApiConnection));
            mockClient.SetupGet(c => c.Migration).Returns(new MigrationClient(mockApiConnection));
            mockClient.SetupGet(c => c.Miscellaneous).Returns(new MiscellaneousClient(connection.Object));
            mockClient.SetupGet(c => c.Oauth).Returns(new OauthClient(connection.Object));
            mockClient.SetupGet(c => c.Organization).Returns(new OrganizationsClient(mockApiConnection));
            mockClient.SetupGet(c => c.PullRequest).Returns(new PullRequestsClient(mockApiConnection));
            mockClient.SetupGet(c => c.Repository).Returns(new RepositoriesClient(mockApiConnection));
            mockClient.SetupGet(c => c.User).Returns(new UsersClient(mockApiConnection));
            mockClient.SetupGet(c => c.Reaction).Returns(new ReactionsClient(mockApiConnection));
            mockClient.SetupGet(c => c.Check).Returns(new ChecksClient(mockApiConnection));
            mockClient.SetupGet(c => c.Search).Returns(mockSearch.Object);


            var optionsSnapshot = new Mock<IOptionsSnapshot<GitHubSearcherConfiguration>>();
            optionsSnapshot
                .Setup(x => x.Value)
                .Returns(
                () => _configuration);

            return new GitHubSearcher(mockClient.Object, new Mock<ILogger<GitHubSearcher>>().Object, optionsSnapshot.Object);
        }

        private static Repository CreateRepository(string fullName, int starCount = 100)
        {
            var ownerName = fullName.Split('/')[0];
            var repoName = fullName.Split('/')[1];
            var owner = new User("", "", "", 0, "", DateTimeOffset.Now, DateTimeOffset.Now, 100, "", 10, 10, true, "", 0, 1, "", ownerName, ownerName, "", 0, null, 0, 0, 0, "", new RepositoryPermissions(), true, "", null);
            return new Repository(
                    "url",
                    "htmlUrl",
                    "cloneUrl",
                    "gitUrl",
                    "sshUrl",
                    "svnUrl",
                    "mirrorUrl",
                    1,// Id
                    "nodeId",
                    owner,
                    repoName,
                    fullName,
                    "description",
                    "homepage",
                    "csharp",
                    false, // Private
                    true, // Fork
                    10, // Fork Count
                    starCount, // Star Count
                    "master", // Default branch
                    0, // Open issues count
                    null, // Pushed at
                    DateTimeOffset.Now, // Created At
                    DateTimeOffset.Now, // Updated At
                    new RepositoryPermissions(),
                    null,
                    null,
                    new LicenseMetadata(),
                    true, // Issues
                    true, // Wiki
                    true, // Downloads
                    true, // Pages
                    10, // Subscriber count
                    500, // Size
                    true, // Allow Rebase merge
                    true, // Allow Squash merge
                    true, // Allow Merge commit
                    false); // Archived?
        }

        public class GetPopularRepositoriesMethod
        {
            [Fact]
            public async Task GetZeroResult()
            {
                var res = await GetMockClient().GetPopularRepositories();
                Assert.Empty(res);
            }

            [Theory]
            [InlineData(4000, 10, 200, 2)] // Tests huge number of pages
            [InlineData(4000, 10, 50, 2)] // Tests huge number of API calls
            [InlineData(30000, 10, 1000, 100)] // Tests huge number of results in real conditions
            public async Task GetMoreThanThousandResults(int totalCount, int minStars, int maxGithubResultPerQuery, int resultsPerPage)
            {
                _configuration.ResultsPerPage = resultsPerPage;
                _configuration.MinStars = minStars;
                _configuration.MaxGitHubResultPerQuery = maxGithubResultPerQuery;

                // Generate ordered results by starCount (the min starCount has to be >= GitHubSearcher.MIN_STARS)
                var items = new List<Repository>();

                int maxStars = (totalCount + _configuration.MinStars);
                for (int i = 0; i < totalCount; i++)
                {
                    items.Add(CreateRepository("owner/Hello" + i, maxStars - i));
                }

                // Create a mock GitHub Search API that serves those results
                Func<SearchRepositoriesRequest, Task<SearchRepositoryResult>> mockGitHubSearch =
                req =>
                    {
                        // Stars are split as "min..max"
                        var starsStr = req.Stars.ToString();
                        var min = int.Parse(starsStr.Substring(0, starsStr.IndexOf('.')));
                        var max = int.Parse(starsStr.Substring(starsStr.LastIndexOf('.') + 1));
                        int idxMax = -1, idxMin = items.Count;

                        for (int i = 0; i < items.Count; i++)
                        {
                            var repo = items[i];
                            if (repo.StargazersCount <= max && idxMax == -1)
                            {
                                idxMax = i;
                            }

                            if (repo.StargazersCount <= min)
                            {
                                idxMin = i;
                                break;
                            }
                        }

                        var itemsCount = Math.Min(_configuration.ResultsPerPage, idxMin - idxMax); // To avoid overflowing
                        var subItems = items.GetRange(idxMax, itemsCount);
                        return Task.FromResult(new SearchRepositoryResult(totalCount, itemsCount == _configuration.ResultsPerPage, subItems));
                    };

                var res = await GetMockClient(mockGitHubSearch).GetPopularRepositories();
                Assert.Equal(items.Count, res.Count);

                for (int resIdx = 0; resIdx < res.Count; resIdx++)
                {
                    var resItem = res[resIdx];
                    Assert.Equal(items[resIdx].Name, resItem.Name);
                    Assert.Equal(items[resIdx].FullName, resItem.Id);
                    Assert.Equal(items[resIdx].StargazersCount, resItem.Stars);
                    Assert.Equal(items[resIdx].Owner.Login, resItem.Owner);
                }
            }
        }

    }
}
