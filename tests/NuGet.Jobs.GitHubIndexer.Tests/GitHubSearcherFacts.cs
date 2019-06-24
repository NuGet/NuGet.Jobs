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
        private static GitHubSearcher GetMockClient(Func<SearchRepositoriesRequest, Task<SearchRepositoryResult>> searchResultFunc = null, GitHubSearcherConfiguration configuration = null)
        {
            var dummyApiInfo = new ApiInfo(
                            new Dictionary<string, Uri>(), // links
                            Array.Empty<string>(), // Oauth scopes
                            Array.Empty<string>(), // accepted Oauth scopes
                            "", // Etag
                            new RateLimit(10, 10, 10));

            var connection = new Mock<IConnection>();
            connection
                .Setup(c => c.GetLastApiInfo())
                .Returns(dummyApiInfo);

            var mockSearchApiRequester = new Mock<IGitHubSearchApiRequester>();
            mockSearchApiRequester
                .Setup(r => r.GetResponse(It.IsAny<IGitHubClient>(), It.IsAny<SearchRepositoriesRequest>()))
                .Returns(async (IGitHubClient client, SearchRepositoriesRequest request) =>
                {
                    return new GitHubSearchApiResponse(searchResultFunc == null ? new SearchRepositoryResult() : await searchResultFunc(request), DateTimeOffset.Now, DateTimeOffset.Now);
                });

            var mockClient = new Mock<IGitHubClient>();
            mockClient.SetupGet(c => c.Connection).Returns(connection.Object);
            var mockApiConnection = new ApiConnection(connection.Object);
            var optionsSnapshot = new Mock<IOptionsSnapshot<GitHubSearcherConfiguration>>();
            optionsSnapshot
                .Setup(x => x.Value)
                .Returns(
                () => configuration ?? new GitHubSearcherConfiguration());

            return new GitHubSearcher(mockClient.Object, mockSearchApiRequester.Object, new Mock<ILogger<GitHubSearcher>>().Object, optionsSnapshot.Object);
        }

        private static Repository CreateRepository(string fullName, int starCount = 100)
        {
            var ownerName = fullName.Split('/')[0];
            var repoName = fullName.Split('/')[1];
            var owner = new User(
                avatarUrl: "",
                bio: "",
                blog: "",
                collaborators: 0,
                company: "",
                createdAt: DateTimeOffset.Now,
                updatedAt: DateTimeOffset.Now,
                diskUsage: 100,
                email: "",
                followers: 10,
                following: 10,
                hireable: true,
                htmlUrl: "",
                totalPrivateRepos: 0,
                id: 1,
                location: "",
                login: ownerName,
                name: ownerName,
                nodeId: "",
                ownedPrivateRepos: 0,
                plan: null,
                privateGists: 0,
                publicGists: 0,
                publicRepos: 0,
                url: "",
                permissions: new RepositoryPermissions(),
                siteAdmin: true,
                ldapDistinguishedName: "",
                suspendedAt: null);

            return new Repository(
                    url: "url",
                    htmlUrl: "htmlUrl",
                    cloneUrl: "cloneUrl",
                    gitUrl: "gitUrl",
                    sshUrl: "sshUrl",
                    svnUrl: "svnUrl",
                    mirrorUrl: "mirrorUrl",
                    id: 1,
                    nodeId: "nodeId",
                    owner: owner,
                    name: repoName,
                    fullName: fullName,
                    description: "description",
                    homepage: "homepage",
                    language: "csharp",
                    @private: false,
                    fork: true,
                    forksCount: 10,
                    stargazersCount: starCount,
                    defaultBranch: "master",
                    openIssuesCount: 0,
                    pushedAt: null,
                    createdAt: DateTimeOffset.Now,
                    updatedAt: DateTimeOffset.Now,
                    permissions: new RepositoryPermissions(),
                    parent: null,
                    source: null,
                    license: new LicenseMetadata(),
                    hasIssues: true,
                    hasWiki: true,
                    hasDownloads: true,
                    hasPages: true,
                    subscribersCount: 10,
                    size: 500,
                    allowRebaseMerge: true,
                    allowSquashMerge: true,
                    allowMergeCommit: true,
                    archived: false);
        }

        public class GetPopularRepositoriesMethod
        {
            private readonly GitHubSearcherConfiguration _configuration = new GitHubSearcherConfiguration();

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

                        var page = req.Page - 1;
                        var startId = idxMax + req.PerPage * page > idxMin ? idxMin : idxMax + req.PerPage * page;

                        var itemsCount = Math.Min(_configuration.ResultsPerPage, idxMin - startId); // To avoid overflowing
                        var subItems = itemsCount == 0 ? new List<Repository>() : items.GetRange(startId, itemsCount);

                        return Task.FromResult(new SearchRepositoryResult(totalCount, itemsCount == _configuration.ResultsPerPage, subItems));
                    };

                var res = await GetMockClient(mockGitHubSearch, _configuration).GetPopularRepositories();
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
