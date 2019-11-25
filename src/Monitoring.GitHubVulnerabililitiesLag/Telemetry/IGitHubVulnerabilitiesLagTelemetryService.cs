// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public interface IGitHubVulnerabilitiesLagTelemetryService
    {
        void TrackGitHubVulnerabilities2DbLag(
            DateTimeOffset eventTime,
            DateTimeOffset cursorValue,
            DateTimeOffset? latestAdvisoryUpdatedAt);
    }
}
