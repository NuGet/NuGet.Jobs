// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault.Models;

namespace NuGet.Services.Metadata.Catalog
{
    public interface IDownloadsV1JsonClient
    {
        Task<DownloadData> ReadAsync(string url);
        Task ReadAsync(string url, Action<string, string, long> addCount);
    }
}