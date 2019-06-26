// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class WritableRepositoryInformation : RepositoryInformation
    {
        public WritableRepositoryInformation(string id, string url, int stars) : base(id, url, stars, Array.Empty<string>())
        {
        }

        public new IReadOnlyList<string> Dependencies { get => WritableDependencies; }
        public List<string> WritableDependencies { get; } = new List<string>();
    }
}
