// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    /// <summary>
    /// Allows accessing <typeparamref name="TNode"/>s returned by GraphQL query.
    /// </summary>
    public class ConnectionResponseData<TNode> where TNode : INode
    {
        public IEnumerable<Edge<TNode>> Edges { get; set; }
    }
}
