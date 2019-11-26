// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    /// <summary>
    /// The nested data inside a <see cref="QueryResponse"/>.
    /// </summary>
    public class QueryResponseData
    {
        /// <summary>
        /// https://developer.github.com/v4/object/securityadvisoryconnection/
        /// </summary>
        public ConnectionResponseData<SecurityAdvisory> SecurityAdvisories { get; set; }
    }
}
