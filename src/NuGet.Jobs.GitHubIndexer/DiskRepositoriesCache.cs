// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class DiskRepositoriesCache : IRepositoriesCache
    {
        private const string CacheFileSuffix = "-Cache.json";

        public void Persist(RepositoryInformation repo)
        {
            var repoFolder = ReposIndexer.ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            var repoCacheFile = repoFolder + CacheFileSuffix;
            File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(repo.Dependencies));
        }

        public bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached)
        {
            var repoFolder = ReposIndexer.ExecutionDirectory + Path.DirectorySeparatorChar + repo.Id;
            var repoCacheFile = repoFolder + CacheFileSuffix;

            cached = null;
            if (File.Exists(repoCacheFile))
            {
                repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
                cached = repo.ToRepositoryInformation();
            }

            return cached != null;
        }
    }
}
