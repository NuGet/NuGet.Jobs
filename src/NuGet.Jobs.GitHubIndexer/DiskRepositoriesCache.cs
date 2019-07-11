// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class DiskRepositoriesCache : IRepositoriesCache
    {
        private const string CacheFileSuffix = "-Cache.json";
        private readonly ILogger<DiskRepositoriesCache> _logger;
        public DiskRepositoriesCache(ILogger<DiskRepositoriesCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Persist(RepositoryInformation repo)
        {
            var repoFolder = ReposIndexer.ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            var repoCacheFile = repoFolder + CacheFileSuffix;
            _logger.LogTrace("Saving cache for repo {RepoId} to file {FileName}", repo.Id, repoCacheFile);
            File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(repo.Dependencies));
        }

        public bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached)
        {
            var repoFolder = ReposIndexer.ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            var repoCacheFile = repoFolder + CacheFileSuffix;

            _logger.LogTrace("Cache lookup for repo {RepoId}", repo.Id);
            cached = null;
            if (File.Exists(repoCacheFile))
            {
                _logger.LogTrace("Cache hit for repo {RepoId} on file {FileName}", repo.Id, repoCacheFile);
                repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
                cached = repo.ToRepositoryInformation();
            }

            return cached != null;
        }
    }
}
