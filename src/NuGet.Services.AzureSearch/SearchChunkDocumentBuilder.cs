// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.Text;
using NuGet.Services.Entities;
using Tiktoken;

namespace NuGet.Services.AzureSearch
{
    public class SearchChunkDocumentBuilder : ISearchChunkDocumentBuilder
    {
        const int MaxTokens = 8191; // for text-embedding-3-small

        private readonly ISearchDocumentBuilder _searchDocumentBuilder;
        private readonly OpenAIClient _openAIClient;
        private readonly Encoding _tokenEncoding;

        public SearchChunkDocumentBuilder(
            ISearchDocumentBuilder searchDocumentBuilder,
            OpenAIClient openAIClient)
        {
            _searchDocumentBuilder = searchDocumentBuilder;
            _openAIClient = openAIClient;
            _tokenEncoding = Tiktoken.Encoding.Get(Encodings.Cl100KBase);
        }

        public async Task<IReadOnlyList<SearchChunkDocument.Full>> FullFromDbAsync(
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
            ConcurrentDictionary<string, ReadOnlyMemory<float>> embeddingCache)
        {
            var chunks = new List<(string Id, string Value)>();

            AddChunks(package.Description, chunks, "description");
            AddChunks(package.ReleaseNotes, chunks, "release_notes");
            AddChunks(package.Summary, chunks, "summary");
            AddChunks(readme, chunks, "readme");

            var embeddingsOptions = new EmbeddingsOptions { DeploymentName = "jver-text-embedding-3-small" };
            var tokenSum = 0;
            var addedIds = new List<string>();
            var addedChunks = new HashSet<string>();

            // get embeddings for all chunks
            foreach (var chunk in chunks)
            {
                if (!addedChunks.Add(chunk.Value))
                {
                    continue;
                }

                var tokens = _tokenEncoding.CountTokens(chunk.Value);
                if (tokenSum + tokens > MaxTokens)
                {
                    await GetEmbeddingsAsync(embeddingsOptions, tokenSum, addedIds, embeddingCache);
                    tokenSum = 0;
                    addedIds.Clear();
                }

                embeddingsOptions.Input.Add(chunk.Value);
                tokenSum += tokens;
                addedIds.Add(chunk.Id);
            }

            if (addedIds.Count > 0)
            {
                await GetEmbeddingsAsync(embeddingsOptions, tokenSum, addedIds, embeddingCache);
            }

            var output = new List<SearchChunkDocument.Full>();

            foreach (var chunk in chunks)
            {
                var document = new SearchChunkDocument.Full();

                _searchDocumentBuilder.FullFromDb(
                    document,
                    packageId,
                    searchFilters,
                    versions,
                    isLatestStable,
                    isLatest,
                    fullVersion,
                    package,
                    owners,
                    totalDownloadCount,
                    isExcludedByDefault);

                document.Key = DocumentUtilities.GetSearchChunkDocumentKey(packageId, searchFilters, chunk.Id);
                document.ChunkVector = embeddingCache[chunk.Value];

                output.Add(document);
            }

            return output;
        }

        private async Task GetEmbeddingsAsync(
            EmbeddingsOptions embeddingsOptions,
            int tokenSum,
            List<string> addedIds,
            ConcurrentDictionary<string, ReadOnlyMemory<float>> embeddingCache)
        {
            var response = await _openAIClient.GetEmbeddingsAsync(embeddingsOptions);
            foreach (var item in response.Value.Data)
            {
                embeddingCache.TryAdd(embeddingsOptions.Input[item.Index], item.Embedding);
            }
        }

        private void AddChunks(string content, List<(string Id, string Value)> chunks, string type)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            const int lineTokens = 40;
            const int paragraphTokens = lineTokens * 4;
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var lines = TextChunker.SplitMarkDownLines(content, lineTokens);
            var paragraphs = TextChunker.SplitMarkdownParagraphs(lines, paragraphTokens, overlapTokens: (int)Math.Round(0.25 * paragraphTokens));
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            for (var i = 0; i < paragraphs.Count; i++)
            {
                chunks.Add((Id: $"{type}-{i}", Value: paragraphs[i]));
            }
        }
    }
}
