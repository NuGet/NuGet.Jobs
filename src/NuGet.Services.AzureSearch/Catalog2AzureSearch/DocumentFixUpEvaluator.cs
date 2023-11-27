// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class DocumentFixUpEvaluator : IDocumentFixUpEvaluator
    {
        private readonly IVersionListDataClient _versionListClient;
        private readonly ICatalogLeafFetcher _leafFetcher;
        private readonly ILogger<DocumentFixUpEvaluator> _logger;
        private readonly ISearchClientWrapper _hijackIndexClient;

        public DocumentFixUpEvaluator(
            IVersionListDataClient versionListClient,
            ICatalogLeafFetcher leafFetcher,
            ILogger<DocumentFixUpEvaluator> logger,
            ISearchClientWrapper hijackIndexClient)
        {
            _versionListClient = versionListClient ?? throw new ArgumentNullException(nameof(versionListClient));
            _leafFetcher = leafFetcher ?? throw new ArgumentNullException(nameof(leafFetcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hijackIndexClient = hijackIndexClient ?? throw new ArgumentNullException(nameof(hijackIndexClient));
        }

        public async Task<DocumentFixUp> TryFixUpAsync(
            IReadOnlyList<CatalogCommitItem> itemList,
            ConcurrentBag<IdAndValue<IndexActions>> allIndexActions,
            InvalidOperationException exception)
        {
            var innerEx = exception.InnerException as IndexBatchException;
            if (innerEx == null || innerEx.IndexingResults == null)
            {
                return DocumentFixUp.IsNotApplicable();
            }

            // There may have been a Case of the Missing Document! We have confirmed with the Azure Search team that
            // this is a bug on the Azure Search side. To mitigate the issue, we replace any Merge operation that
            // failed with 404 with a MergeOrUpload with the full metadata so that we can replace that missing document.
            //
            // 1. The first step is to find all of the document keys that failed with a 404 Not Found error.
            var notFoundKeys = new HashSet<string>(innerEx
                .IndexingResults
                .Where(x => x.Status == (int)HttpStatusCode.NotFound)
                .Select(x => x.Key));
            if (!notFoundKeys.Any())
            {
                return DocumentFixUp.IsNotApplicable();
            }

            _logger.LogWarning("{Count} document action(s) failed with 404 Not Found.", notFoundKeys.Count);

            // 2. Find all of the package IDs that were affected, considering Merge operations against the Search
            //    and Hijack index.
            var failedIdsForSearch = new HashSet<string>();
            var failedIdsForHijack = new HashSet<string>();
            foreach (var pair in allIndexActions.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                var failedMergesSearch = pair
                    .Value
                    .Search
                    .Where(a => a.ActionType == IndexActionType.Merge)
                    .Where(a => notFoundKeys.Contains(a.Document.Key));

                var failedMessagesHijack = pair
                    .Value
                    .Hijack
                    .Where(a => a.ActionType == IndexActionType.Merge)
                    .Where(a => notFoundKeys.Contains(a.Document.Key)); 

                //var failedMerges = failedMergesSearch.Concat(failedMessagesHijack);

                if (failedMergesSearch.Any() && failedIdsForSearch.Add(pair.Id))
                {
                    _logger.LogWarning("Package {PackageId} had a Merge operation fail with 404 Not Found.", pair.Id);
                }

                if (failedMessagesHijack.Any() && failedIdsForSearch.Add(pair.Id))
                {
                    _logger.LogWarning("Package {PackageId} had a Merge operation fail with 404 Not Found.", pair.Id);
                }
            }


            if (!failedIdsForSearch.Any() && !failedIdsForHijack.Any())
            {
                _logger.LogInformation("No failed Merge operations against the Search index and Hijack index were found.");
                return DocumentFixUp.IsNotApplicable();
            }

                var newItemList = new List<CatalogCommitItem>();
            if (failedIdsForSearch.Any()) { 

                _logger.LogInformation(
                    "{Count} package(s) had a Merge operation fail with 404 Not Found against the Search index.",
                    failedIdsForSearch.Count);
                var searchItemList = await DocumentFixUpForSearchIndex(itemList, failedIdsForSearch);
                newItemList.AddRange(searchItemList);
            }

            if (failedIdsForHijack.Any()) { 
                _logger.LogInformation(
                    "{Count} package(s) had a Merge operation fail with 404 Not Found against the Hijack index.",
                    failedIdsForHijack.Count);
                var hijackItemList = await DocumentFixUpForHijackIndex(itemList, failedIdsForSearch);
                newItemList.AddRange(hijackItemList);
            }

            return DocumentFixUp.IsApplicable(newItemList);
        }

        public async Task<List<CatalogCommitItem>> DocumentFixUpForSearchIndex(IReadOnlyList<CatalogCommitItem> itemList, HashSet<string> failedIdsForSearch)
        {
            // 3. For each affected package ID, get the version list to determine the latest version per search filter
            //    so we can find the the catalog entry for the version.
            var identityToItems = itemList.GroupBy(x => x.PackageIdentity).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var packageId in failedIdsForSearch)
            {
                var accessConditionAndData = await _versionListClient.ReadAsync(packageId);
                var versionLists = new VersionLists(accessConditionAndData.Result);

                var latestVersions = DocumentUtilities
                    .AllSearchFilters
                    .Select(sf => versionLists.GetLatestVersionInfoOrNull(sf))
                    .Where(lvi => lvi != null)
                    .Select(lvi => (IReadOnlyList<NuGetVersion>)new List<NuGetVersion> { lvi.ParsedVersion })
                    .ToList();

                var leaves = await _leafFetcher.GetLatestLeavesAsync(packageId, latestVersions);

                // We ignore unavailable (deleted) versions for now. We have never had a delete cause this problem. It's
                // only ever been discovered when a new version is being added or updated.
                //
                // For each package details leaf found, create a catalog commit item and add it to the set of items we
                // will process. This will force the metadata to be updated on each of the latest versions. Since this
                // is the latest metadata, replace any older leaves that may be associated with that package version.
                foreach (var pair in leaves.Available)
                {
                    var identity = new PackageIdentity(packageId, pair.Key);
                    var leaf = pair.Value;

                    if (identityToItems.TryGetValue(identity, out var existing))
                    {
                        if (existing.Count == 1 && existing[0].Uri.AbsoluteUri == leaf.Url)
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata will remain the same.",
                                identity.Id,
                                identity.Version.ToNormalizedString());
                            continue;
                        }
                        else
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata from {Url} will be used instead of {Count} catalog commit items.",
                                identity.Id,
                                identity.Version.ToNormalizedString(),
                                leaf.Url,
                                existing.Count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "For {PackageId} {PackageVersion}, metadata from {Url} will be used.",
                            identity.Id,
                            identity.Version.ToNormalizedString(),
                            leaf.Url);
                    }

                    identityToItems[identity] = new List<CatalogCommitItem>
                    {
                        new CatalogCommitItem(
                            new Uri(leaf.Url),
                            leaf.CommitId,
                            leaf.CommitTimestamp.UtcDateTime,
                            new string[0],
                            new[] { Schema.DataTypes.PackageDetails },
                            identity),
                    };
                }
            }

            _logger.LogInformation("The catalog commit item list has been modified to fix up the missing document(s) in Search Index.");

            var newItemList = identityToItems.SelectMany(x => x.Value).ToList();
            return newItemList;
        }

        public async Task<List<CatalogCommitItem>> DocumentFixUpForHijackIndex(IReadOnlyList<CatalogCommitItem> itemList, HashSet<string> failedIdsForHijack)
        {
            // 3. For each affected package ID, get the version list to determine the latest version per search filter
            //    so we can find the the catalog entry for the version.
            var identityToItems = itemList.GroupBy(x => x.PackageIdentity).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var packageId in failedIdsForHijack)
            {
                var accessConditionAndData = await _versionListClient.ReadAsync(packageId);
        
                var versionListData = accessConditionAndData.Result;

                var notFoundVersions = new List<NuGetVersion>();
                foreach (var version in versionListData.VersionProperties)
                {
                    //Get hijack index document 
                    var hijackIndexDocumentKey = DocumentUtilities.GetHijackDocumentKey(packageId, version.Key);
                    var hijackIndexDocument = await _hijackIndexClient.GetOrNullAsync<HijackDocument.Full>(hijackIndexDocumentKey);

                    if (hijackIndexDocument == null)
                    {
                        _logger.LogInformation(
                            "For {PackageId} {PackageVersion}, hijack index document is not found.",
                            packageId,
                            version.Key);
                        notFoundVersions.Add(new NuGetVersion(version.Key));

                    }
                }

                var latestVersions = notFoundVersions
                    .Select(v => (IReadOnlyList<NuGetVersion>)new List<NuGetVersion> { v })
                    .ToList();

                var leaves = await _leafFetcher.GetLatestLeavesAsync(packageId, latestVersions);

                // We ignore unavailable (deleted) versions for now. We have never had a delete cause this problem. It's
                // only ever been discovered when a new version is being added or updated.
                //
                // For each package details leaf found, create a catalog commit item and add it to the set of items we
                // will process. This will force the metadata to be updated on each of the latest versions. Since this
                // is the latest metadata, replace any older leaves that may be associated with that package version.
                foreach (var pair in leaves.Available)
                {
                    var identity = new PackageIdentity(packageId, pair.Key);
                    var leaf = pair.Value;

                    if (identityToItems.TryGetValue(identity, out var existing))
                    {
                        if (existing.Count == 1 && existing[0].Uri.AbsoluteUri == leaf.Url)
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata will remain the same.",
                                identity.Id,
                                identity.Version.ToNormalizedString());
                            continue;
                        }
                        else
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata from {Url} will be used instead of {Count} catalog commit items.",
                                identity.Id,
                                identity.Version.ToNormalizedString(),
                                leaf.Url,
                                existing.Count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "For {PackageId} {PackageVersion}, metadata from {Url} will be used.",
                            identity.Id,
                            identity.Version.ToNormalizedString(),
                            leaf.Url);
                    }

                    identityToItems[identity] = new List<CatalogCommitItem>
                    {
                        new CatalogCommitItem(
                            new Uri(leaf.Url),
                            leaf.CommitId,
                            leaf.CommitTimestamp.UtcDateTime,
                            new string[0],
                            new[] { Schema.DataTypes.PackageDetails },
                            identity),
                    };
                }
                }            

            _logger.LogInformation("The catalog commit item list has been modified to fix up the missing document(s) in Hijack Index.");

            var newItemList = identityToItems.SelectMany(x => x.Value).ToList();
            return newItemList;
        }
    }
}
