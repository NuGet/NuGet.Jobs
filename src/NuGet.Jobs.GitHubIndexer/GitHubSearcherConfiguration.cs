// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcherConfiguration
    {
        public int MinStars { get; set; } = 100;

        public int ResultsPerPage { get; set; } = 100;

        public int MaxGithubResultPerQuery { get; set; } = 1000;

    }
}
