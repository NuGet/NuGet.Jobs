using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcher
    {
        private GitHubClient _client;

        public GitHubSearcher()
        {
            _client = new GitHubClient(new ProductHeaderValue("GitHubIndexer"));
        }

        private async Task<List<Repository>> GetResultsForPage(int currPage, int totalCount, int maxStarCount = -1, string lastRecordName = null)
        {
            const int MIN_STARS = 100;
            if (_client.GetLastApiInfo() != null && _client.GetLastApiInfo().RateLimit.Remaining == 0)
            {
                Console.WriteLine("Waiting a minute to cooldown..");
                await Task.Delay(TimeSpan.FromSeconds(61));
                Console.WriteLine("Resuming query =D");
            }

            var request = new SearchRepositoriesRequest
            {
                Stars = maxStarCount == -1 ? Range.GreaterThan(MIN_STARS) : new Range(MIN_STARS, maxStarCount),
                Language = Language.CSharp,
                SortField = RepoSearchSort.Stars,
                Order = SortDirection.Descending,
                PerPage = 100, // Maximum is 100 :(
                Page = currPage
            };

            List<Repository> resultList = new List<Repository>();

            SearchRepositoryResult response = _client.Search.SearchRepo(request).GetAwaiter().GetResult();
            if (response.Items.Count > 0)
            {
                var toAdd =
                    lastRecordName == null ?
                        response.Items :
                        response.Items.Where(repo => repo.FullName != lastRecordName);
                resultList.AddRange(toAdd);

                // Since there can only be 100 results per page, if the count is 100, it means we should query the next page
                if (response.Items.Count == 100)
                {
                    ++currPage;

                    if (currPage <= 10)
                    {
                        //resultList.UnionWith(GetResultsForPage(currPage, resultList.Count + totalCount, maxStarCount));
                        resultList.AddRange( await GetResultsForPage(currPage, resultList.Count + totalCount, maxStarCount));
                    }
                    else
                    {
                        // Since we need to grab more than 1000 results, let's pick up where the $currLast$ repository is and build a new query from there
                        // This will make us count from the $recursivePage$ parameter and not the currPage anymore
                        //resultList.UnionWith(GetResultsForPage(1, resultList.Count + totalCount, response.Items[response.Items.Count - 1].StargazersCount));
                        resultList.AddRange(await GetResultsForPage(1, resultList.Count + totalCount, response.Items[response.Items.Count - 1].StargazersCount, response.Items[response.Items.Count - 1].FullName));
                    }
                }
            }

            return resultList;
        }

        /// <summary>
        /// Searches for all the C# repos that have more than 100 stars on GitHub, orders them in Descending order and returns the first 100 matches
        /// </summary>
        /// <returns>First 100 C# repos on GitHub that have more than 100 stars</returns>
        public async Task<List<Repository>> GetRepos()
        {
            var result = await GetResultsForPage(1, 0);
            return result;
        }
    }
}
