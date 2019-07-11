// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IRepositoriesCache
    {
        bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached);

        void Persist(RepositoryInformation repo);
    }
}
