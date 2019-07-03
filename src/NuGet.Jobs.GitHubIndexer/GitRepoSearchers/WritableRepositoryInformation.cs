// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class WritableRepositoryInformation
    {
        private readonly HashSet<string> _writableDependencies = new HashSet<string>(); // Using a HashSet to avoid duplicates

        public WritableRepositoryInformation(string id, string url, int stars, string mainBranch)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Stars = stars;
            MainBranch = mainBranch ?? throw new ArgumentNullException(nameof(mainBranch));
        }

        public string MainBranch { get; }

        public string Id { get; }

        public string Url { get; }

        public int Stars { get; }

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
        }

        public RepositoryInformation ToRepositoryInformation()
        {
            return new RepositoryInformation(Id, Url, Stars, _writableDependencies.ToList());
        }
    }
}
