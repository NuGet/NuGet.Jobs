// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class PackageEntityIndexActionBuilder : IPackageEntityIndexActionBuilder
    {
        private readonly ISearchDocumentBuilder _search;
        private readonly ISearchChunkDocumentBuilder _searchChunk;
        private readonly IHijackDocumentBuilder _hijack;
        private readonly ILogger<PackageEntityIndexActionBuilder> _logger;

        public PackageEntityIndexActionBuilder(
            ISearchDocumentBuilder search,
            ISearchChunkDocumentBuilder searchChunk,
            IHijackDocumentBuilder hijack,
            ILogger<PackageEntityIndexActionBuilder> logger)
        {
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _searchChunk = searchChunk ?? throw new ArgumentNullException(nameof(searchChunk));
            _hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IndexActions> AddNewPackageRegistrationAsync(NewPackageRegistration packageRegistration)
        {
            var versionProperties = new Dictionary<string, VersionPropertiesData>();
            var versionListData = new VersionListData(versionProperties);
            var versionLists = new VersionLists(versionListData);

            var changes = packageRegistration
                .Packages
                .Select(GetVersionListChange)
                .ToList();
            var indexChanges = versionLists.ApplyChanges(changes);

            var versionToPackage = packageRegistration
                .Packages
                .ToDictionary(p => NuGetVersion.Parse(p.Version));

            var search = indexChanges
                .Search
                .Select(p => GetSearchIndexAction(
                    packageRegistration,
                    versionToPackage,
                    versionLists,
                    p.Key,
                    p.Value))
                .ToList();

            var searchChunks = new List<IndexDocumentsAction<KeyedDocument>>();
            var embeddingCache = new ConcurrentDictionary<string, ReadOnlyMemory<float>>();
            foreach (var indexChange in indexChanges.Search)
            {
                searchChunks.AddRange(await GetSearchChunkIndexActionsAsync(
                    packageRegistration,
                    versionToPackage,
                    packageRegistration.VersionToReadme,
                    versionLists,
                    indexChange.Key,
                    indexChange.Value,
                    embeddingCache));
            }

            var hijack = indexChanges
                .Hijack
                .Select(p => GetHijackIndexAction(
                    packageRegistration.PackageId,
                    versionToPackage[p.Key],
                    p.Value))
                .ToList();

            return new IndexActions(
                search,
                searchChunks,
                hijack,
                new ResultAndAccessCondition<VersionListData>(
                    versionLists.GetVersionListData(),
                    AccessConditionWrapper.GenerateEmptyCondition()));
        }

        private void VerifyConsistency(
            string packageId,
            Package package)
        {
            var parsedVersion = NuGetVersion.Parse(package.Version);
            var normalizedString = parsedVersion.ToNormalizedString();

            if (package.NormalizedVersion == null
                || package.NormalizedVersion != parsedVersion.ToNormalizedString())
            {
                var message = $"The calculated {nameof(Package.NormalizedVersion)} does not match the DB value.";
                _logger.LogError(
                    message + ". ID: {PackageId}, DB: {DbValue}, Calculated: {CalculatedValue}",
                    packageId,
                    package.NormalizedVersion,
                    normalizedString);
                throw new InvalidOperationException(message);
            }

            if (package.IsPrerelease != parsedVersion.IsPrerelease)
            {
                var message = $"The calculated {nameof(Package.IsPrerelease)} does not match the DB value.";
                _logger.LogError(
                    message + ". ID: {PackageId}, DB: {DbValue}, Calculated: {CalculatedValue}",
                    packageId,
                    package.IsPrerelease,
                    parsedVersion.IsPrerelease);
                throw new InvalidOperationException(message);
            }
        }

        private static VersionListChange GetVersionListChange(Package x)
        {
            return VersionListChange.Upsert(
                fullOrOriginalVersion: x.Version,
                data: new VersionPropertiesData(
                    listed: x.Listed,
                    semVer2: x.SemVerLevelKey.HasValue && x.SemVerLevelKey.Value >= SemVerLevelKey.SemVer2));
        }

        private IndexDocumentsAction<KeyedDocument> GetSearchIndexAction(
            NewPackageRegistration packageRegistration,
            IReadOnlyDictionary<NuGetVersion, Package> versionToPackage,
            VersionLists versionLists,
            SearchFilters searchFilters,
            SearchIndexChangeType changeType)
        {
            if (changeType == SearchIndexChangeType.Delete)
            {
                return IndexDocumentsAction.Delete(_search.Keyed(
                    packageRegistration.PackageId,
                    searchFilters));
            }

            if (changeType != SearchIndexChangeType.AddFirst)
            {
                throw new ArgumentException(
                     $"The only change types supported are {nameof(SearchIndexChangeType.AddFirst)} and " +
                     $"{nameof(SearchIndexChangeType.Delete)}.",
                     nameof(changeType));
            }

            var latestFlags = _search.LatestFlagsOrNull(versionLists, searchFilters);
            var package = versionToPackage[latestFlags.LatestVersionInfo.ParsedVersion];
            var owners = packageRegistration
                .Owners
                .OrderBy(u => u, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();

            VerifyConsistency(packageRegistration.PackageId, package);

            return IndexDocumentsAction.Upload<KeyedDocument>(_search.FullFromDb(
                packageRegistration.PackageId,
                searchFilters,
                latestFlags.LatestVersionInfo.ListedFullVersions,
                latestFlags.IsLatestStable,
                latestFlags.IsLatest,
                latestFlags.LatestVersionInfo.FullVersion,
                package,
                owners,
                packageRegistration.TotalDownloadCount,
                packageRegistration.IsExcludedByDefault));
        }

        private async Task<IReadOnlyList<IndexDocumentsAction<KeyedDocument>>> GetSearchChunkIndexActionsAsync(
            NewPackageRegistration packageRegistration,
            IReadOnlyDictionary<NuGetVersion, Package> versionToPackage,
            IReadOnlyDictionary<NuGetVersion, string> versionToReadme,
            VersionLists versionLists,
            SearchFilters searchFilters,
            SearchIndexChangeType changeType,
            ConcurrentDictionary<string, ReadOnlyMemory<float>> embeddingCache)
        {
            if (changeType == SearchIndexChangeType.Delete)
            {
                // TODO: implement delete
                return Array.Empty<IndexDocumentsAction<KeyedDocument>>();
            }

            if (changeType != SearchIndexChangeType.AddFirst)
            {
                throw new ArgumentException(
                     $"The only change types supported are {nameof(SearchIndexChangeType.AddFirst)} and " +
                     $"{nameof(SearchIndexChangeType.Delete)}.",
                     nameof(changeType));
            }

            var latestFlags = _search.LatestFlagsOrNull(versionLists, searchFilters);
            var package = versionToPackage[latestFlags.LatestVersionInfo.ParsedVersion];
            var owners = packageRegistration
                .Owners
                .OrderBy(u => u, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
            versionToReadme.TryGetValue(latestFlags.LatestVersionInfo.ParsedVersion, out var readme);

            VerifyConsistency(packageRegistration.PackageId, package);

            var chunks = await _searchChunk.FullFromDbAsync(
                packageRegistration.PackageId,
                searchFilters,
                latestFlags.LatestVersionInfo.ListedFullVersions,
                latestFlags.IsLatestStable,
                latestFlags.IsLatest,
                latestFlags.LatestVersionInfo.FullVersion,
                package,
                readme,
                owners,
                packageRegistration.TotalDownloadCount,
                packageRegistration.IsExcludedByDefault,
                embeddingCache);

            // TODO: should just be "Upload"
            return chunks.Select(x => IndexDocumentsAction.MergeOrUpload<KeyedDocument>(x)).ToList();
        }

        private IndexDocumentsAction<KeyedDocument> GetHijackIndexAction(
            string packageId,
            Package package,
            HijackDocumentChanges changes)
        {
            if (!changes.UpdateMetadata)
            {
                throw new ArgumentException(
                    "The hijack document changes must be set to update metadata.",
                    nameof(changes));
            }

            VerifyConsistency(packageId, package);

            return IndexDocumentsAction.Upload<KeyedDocument>(_hijack.FullFromDb(
                packageId,
                changes,
                package));
        }
    }
}
