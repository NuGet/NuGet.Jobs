// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface ISearchChunkDocumentBuilder
    {
        Task<IReadOnlyList<SearchChunkDocument.Full>> FullFromDbAsync(
            string packageId, 
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            Package package,
            string readme,
            string[] owners,
            long totalDownloadCount,
            bool isExcludedByDefault,
            ConcurrentDictionary<string, ReadOnlyMemory<float>> embeddingCache);
    }
}