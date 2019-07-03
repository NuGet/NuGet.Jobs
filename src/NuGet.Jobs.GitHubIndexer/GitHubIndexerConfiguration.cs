// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubIndexerConfiguration
    {
        /// <summary>
        /// Minimum number of stars that a GitHub Repo needs to have to be included in the indexing
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 32;
    }
}
