// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearcherFacts
    {
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

            return new GitHubSearcher(mockClient.Object);
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

            [Fact]
            public async Task GetMoreThanThousandResults()
            {
                // Generate ordered results by starCount (the min starCount has to be >= GitHubSearcher.MIN_STARS)
                var items = new List<Repository>();
                const int totalCount = 4000;
                const int maxStars = (totalCount + GitHubSearcher.MIN_STARS);
                for (int i = 0; i < totalCount; i++)
                {
                    items.Add(CreateRepository("owner/Hello" + i, maxStars - i));
                }

                // Create a mock GitHub Search API that serves those results
                Func<SearchRepositoriesRequest, Task<SearchRepositoryResult>> mockGitHubSearch =
                req =>
                    {
                        var isRange = req.Stars.ToString().Contains("..");
                        var index = (req.Page - 1) * GitHubSearcher.RESULTS_PER_PAGE;

                        // The user is asking for a min..max range of stars
                        if (isRange)
                        {
                            var str = req.Stars.ToString();
                            var max = str.Substring(str.LastIndexOf('.') + 1);
                            index += maxStars - int.Parse(max);
                        }
                        var itemsCount = Math.Min(GitHubSearcher.RESULTS_PER_PAGE, items.Count - index); // To avoid overflowing
                        var subItems = items.GetRange(index, itemsCount);
                        return Task.FromResult(new SearchRepositoryResult(totalCount, itemsCount == 100, subItems));
                    };

                var res = await GetMockClient(mockGitHubSearch).GetPopularRepositories();
                Assert.Equal(items.Count, res.Count);

                int resIdx = 0;
                foreach (var resItem in res)
                {
                    Assert.Equal(items[resIdx].Name, resItem.Name);
                    Assert.Equal(items[resIdx].FullName, resItem.Id);
                    Assert.Equal(items[resIdx].StargazersCount, resItem.Stars);
                    Assert.Equal(items[resIdx].Owner.Login, resItem.Owner);
                    resIdx++;
                }
            }
        }

    }
}
