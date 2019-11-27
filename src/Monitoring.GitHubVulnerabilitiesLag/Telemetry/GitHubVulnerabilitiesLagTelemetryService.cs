// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public class GitHubVulnerabilitiesLagTelemetryService : IGitHubVulnerabilitiesLagTelemetryService
    {
        private readonly ITelemetryClient _telemetryClient;

        private const string CursorValue = nameof(CursorValue);
        private const string GitHubVulnerabilities2DbLagInSeconds = nameof(GitHubVulnerabilities2DbLagInSeconds);
        private const string LatestAdvisoryUpdatedAt = nameof(LatestAdvisoryUpdatedAt);
        private const string LastCheckedAt = nameof(LastCheckedAt);
        private const string TimeSinceLastGitHubVulnerabilityPublished = nameof(TimeSinceLastGitHubVulnerabilityPublished);

        public GitHubVulnerabilitiesLagTelemetryService(
            ITelemetryClient telemetryClient,
            MonitoringConfiguration configuration)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackGitHubVulnerabilities2DbLag(DateTimeOffset eventTime, DateTimeOffset cursorValue, DateTimeOffset? latestAdvisoryUpdatedAt)
        {
            var latestAdvisoryUpdatedAtString = latestAdvisoryUpdatedAt.HasValue ? latestAdvisoryUpdatedAt.Value.ToString("O") : string.Empty;

            // Tracks the lag in seconds between successful monitor check.
            //
            // When no new advisories have been retrieved from the GitHub API, we'll reset the lag to zero seconds
            // to indicate that the monitor successfully ran but did not yield any new advisories to be processed.
            //
            // Otherwise, the lag will be the total seconds between the time the latest advisory was updated on the GH API,
            // and the cursor value.
            var lagInSeconds = latestAdvisoryUpdatedAt == null ? 0 : (latestAdvisoryUpdatedAt.Value - cursorValue).TotalSeconds;

            var metadata = new Dictionary<string, string>
            {
                { CursorValue, cursorValue.ToString("O") },
                { LatestAdvisoryUpdatedAt, latestAdvisoryUpdatedAtString },
                { LastCheckedAt, eventTime.ToString("O")}
            };

            _telemetryClient.TrackMetric(
                GitHubVulnerabilities2DbLagInSeconds,
                lagInSeconds,
                metadata);

            // A separate metric tracking the time since the last known security advisory affecting NuGet packages was published on GitHub's API.
            var timeInSecondsSinceLastGitHubVulnerabilityPublished = latestAdvisoryUpdatedAt.HasValue ? lagInSeconds : (eventTime - cursorValue).TotalSeconds;

            _telemetryClient.TrackMetric(
                TimeSinceLastGitHubVulnerabilityPublished,
                timeInSecondsSinceLastGitHubVulnerabilityPublished,
                metadata);
        }
    }
}
