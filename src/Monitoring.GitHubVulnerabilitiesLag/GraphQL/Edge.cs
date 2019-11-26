// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Monitoring.GitHubVulnerabilitiesLag
{
    /// <summary>
    /// Wraps a <typeparamref name="TNode"/> with its <see cref="Cursor"/>.
    /// </summary>
    public class Edge<TNode> where TNode : INode
    {
        public TNode Node { get; set; }
    }
}