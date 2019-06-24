// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchApiResponse
    {
        public GitHubSearchApiResponse(SearchRepositoryResult result, DateTimeOffset date, DateTimeOffset throttleResetTime)
        {
            Result = result;
            Date = date;
            ThrottleResetTime = throttleResetTime;
        }

        public SearchRepositoryResult Result { get; }
        public DateTimeOffset Date { get; }
        public DateTimeOffset ThrottleResetTime { get; }
    }
}
