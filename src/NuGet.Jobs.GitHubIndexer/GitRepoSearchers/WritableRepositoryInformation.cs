// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class WritableRepositoryInformation : RepositoryInformation
    {
        private readonly List<string> _writableDependencies = new List<string>();

        public WritableRepositoryInformation(string id, string url, int stars) : base(id, url, stars, Array.Empty<string>())
        {
        }

        public void AddDependency(string dependency)
        {
            _writableDependencies.Add(dependency);
        }

        public void AddDependencies(IEnumerable<string> dependencies)
        {
            _writableDependencies.AddRange(dependencies);
        }

        public new IReadOnlyList<string> Dependencies => _writableDependencies;
    }
}
