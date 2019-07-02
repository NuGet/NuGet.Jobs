// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class WritableRepositoryInformation : RepositoryInformation
    {
        private readonly HashSet<string> _writableDependencies = new HashSet<string>();

        public WritableRepositoryInformation(string id, string url, int stars, string mainBranch) : base(id, url, stars, Array.Empty<string>())
        {
            MainBranch = mainBranch ?? throw new ArgumentNullException(nameof(mainBranch));
        }

        public string MainBranch { get; }

        public void AddDependency(string dependency)
        {
            _writableDependencies.Add(dependency);
        }

        public void AddDependencies(IEnumerable<string> dependencies)
        {
            foreach(var elem in dependencies)
            {
                _writableDependencies.Add(elem);
            }
            //_writableDependencies.AddRange(dependencies);
        }

        public new IReadOnlyList<string> Dependencies => _writableDependencies.ToList();
    }
}
