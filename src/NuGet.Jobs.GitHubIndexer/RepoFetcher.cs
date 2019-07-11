// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class RepoFetcher : IRepoFetcher
    {
        private readonly RepoUtils _repoUtils;
        private readonly ILogger<RepoFetcher> _logger;

        public RepoFetcher(RepoUtils repoUtils, ILogger<RepoFetcher> logger)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IFetchedRepo FetchRepo(WritableRepositoryInformation repo)
        {
            return FetchedRepo.GetInstance(repo, _repoUtils, _logger);
        }
    }
}
