// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.GitHubIndexer
{
    public class RepoFetcher : IRepoFetcher
    {
        private readonly RepoUtils _repoUtils;

        public RepoFetcher(RepoUtils repoUtils)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
        }

        public IFetchedRepo FetchRepo(WritableRepositoryInformation repo)
        {
            return FetchedRepo.GetInstance(repo, _repoUtils);
        }
    }
}
