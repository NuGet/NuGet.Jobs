// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitReposSearcher
    {
        private IReadOnlyCollection<IGitRepoSearcher> _searchers;
        public GitReposSearcher()
        {
            _searchers = new IGitRepoSearcher[]
            {
                new GitHubSearcher()
            };
        }

        /// <summary>
        /// Gets a list of popular C# Git repositories from multiple data sources
        /// </summary>
        /// <returns>List of popular C# Git repos</returns>
        public async Task<IReadOnlyCollection<RepositoryInformation>> GetPopularRepositories()
        {
            var resultList = new List<RepositoryInformation>();

            // TODO: Make this run in parallel
            foreach (var searcher in _searchers)
            {
                resultList.AddRange(await searcher.GetPopularRepositories());
            }

            return resultList;
        }
    }
}
