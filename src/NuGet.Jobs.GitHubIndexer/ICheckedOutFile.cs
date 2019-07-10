// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface ICheckedOutFile
    {
        Stream openFile();

        string Path { get; }

        string RepoId { get; }
    }
}
