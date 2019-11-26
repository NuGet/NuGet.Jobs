// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    public class GitHubVulnerabilitiesLagMonitorConfiguration
    {
        /// <summary>
        /// GitHub's v4 GraphQL API endpoint.
        /// </summary>
        public Uri GitHubGraphQLQueryEndpoint { get; set; } = new Uri("https://api.github.com/graphql");

        /// <summary>
        /// The personal access token to use to authenticate with GitHub.
        /// </summary>
        public string GitHubPersonalAccessToken { get; set; }

        /// <summary>
        /// The storage connection to use to retrieve the GitHubVulnerabilities2Db job's cursor.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The storage container to retrieve the GitHubVulnerabilities2Db job's cursor from.
        /// </summary>
        public string CursorContainerName { get; set; } = "vulnerability";

        /// <summary>
        /// The name of the blob to retrieve the GitHubVulnerabilities2Db job's advisories cursor from.
        /// </summary>
        public string AdvisoryCursorBlobName { get; set; } = "cursor.json";
    }
}
