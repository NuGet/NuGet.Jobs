// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using static NuGet.Jobs.GitHubIndexer.RepoUtils;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IFetchedRepo : IDisposable
    {
        List<GitFileInfo> GetFileInfos();

        List<ICheckedOutFile> CheckoutFiles(IReadOnlyCollection<string> filePaths);
    }
}
