// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Search.Documents.Indexes;
using NuGet.Services.AzureSearch.ScoringProfiles;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The different models for reading from and writing to the search index.
    /// </summary>
    public static class SearchChunkDocument
    {
        /// <summary>
        /// All fields available in the search index. Used for reading the index and updating the index from database,
        /// which has all fields available (as opposed to the catalog, which does not have all fields, like total
        /// download count).
        /// </summary>
        public class Full : SearchDocument.Full
        {
            [VectorSearchField(VectorSearchDimensions = DefaultVectorSearch.Dimensions, VectorSearchProfileName = DefaultVectorSearch.Name)]
            public ReadOnlyMemory<float> ChunkVector { get; set; }
        }

        /// <summary>
        /// Used when processing <see cref="SearchIndexChangeType.AddFirst"/>,
        /// <see cref="SearchIndexChangeType.UpdateLatest"/> or <see cref="SearchIndexChangeType.DowngradeLatest"/>.
        /// </summary>
        public class UpdateLatest : SearchDocument.UpdateLatest
        {
            [VectorSearchField(VectorSearchDimensions = DefaultVectorSearch.Dimensions, VectorSearchProfileName = DefaultVectorSearch.Name)]
            public ReadOnlyMemory<float> ChunkVector { get; set; }
        }
    }
}
