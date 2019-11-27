// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public interface IGitHubQueryService
    {
        /// <summary>
        /// Returns the 'UpdatedAt' <see cref="DateTimeOffset"/> value for the latest GitHub security advisory, 
        /// or <c>null</c> if no new advisories are found beyond the provided <paramref name="cursorValue"/>.
        /// </summary>
        /// <param name="cursorValue">The cursor value of the last processed GitHub security advisory.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
        Task<DateTimeOffset?> GetLatestAdvisoryUpdateAsync(DateTimeOffset cursorValue, CancellationToken cancellationToken);
    }
}
