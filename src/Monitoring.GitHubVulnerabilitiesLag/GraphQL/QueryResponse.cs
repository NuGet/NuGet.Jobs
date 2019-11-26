// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    /// <summary>
    /// A GraphQL query response object.
    /// </summary>
    public class QueryResponse
    {
        public QueryResponseData Data { get; set; }
    }
}
