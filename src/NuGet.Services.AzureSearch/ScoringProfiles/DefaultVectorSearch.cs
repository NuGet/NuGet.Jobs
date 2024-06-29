// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes.Models;

namespace NuGet.Services.AzureSearch.ScoringProfiles
{
    public static class DefaultVectorSearch
    {
        public const string Name = "nuget_vector_search";
        public const string AlgorithName = "hnsw_default";
        public const int Dimensions = 1536; // OpenAI's text-embedding-3-small

        public static VectorSearch Create()
        {
            return new VectorSearch
            {
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(AlgorithName)
                    {
                        Parameters = new HnswParameters
                        {
                            EfConstruction = 400,
                            EfSearch = 500,
                            M = 4,
                            Metric = VectorSearchAlgorithmMetric.Cosine,
                        },
                    }
                },
                Profiles =
                {
                    new VectorSearchProfile(Name, AlgorithName)
                },
            };
        }
    }
}
