﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilder : ISearchDocumentBuilder
    {
        private readonly string[] ImpliedDependency = new string[] { "Dependency" };
        private readonly string[] FilterableImpliedDependency = new string[] { "dependency" };

        private readonly IBaseDocumentBuilder _baseDocumentBuilder;

        public SearchDocumentBuilder(IBaseDocumentBuilder baseDocumentBuilder)
        {
            _baseDocumentBuilder = baseDocumentBuilder ?? throw new ArgumentNullException(nameof(baseDocumentBuilder));
        }

        public SearchDocument.LatestFlags LatestFlagsOrNull(VersionLists versionLists, SearchFilters searchFilters)
        {
            var latest = versionLists.GetLatestVersionInfoOrNull(searchFilters);
            if (latest == null)
            {
                return null;
            }

            // The latest version, given the "include prerelease" bit of the search filter, may or may not be the
            // absolute latest version when considering both prerelease and stable versions. Consider the following
            // cases:
            //
            // Case #1:
            //   SearchFilters.Default:
            //     All versions: 1.0.0, 2.0.0-alpha
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = false
            //
            // Case #2:
            //   SearchFilters.Default:
            //     All versions: 1.0.0
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = true
            //
            // Case #3:
            //   SearchFilters.IncludePrerelease:
            //     All versions: 1.0.0, 2.0.0-alpha
            //     Latest version given filters: 2.0.0-alpha
            //     V2 search document flags:
            //       IsLatestStable = false
            //       IsLatest       = true
            //
            // Case #4:
            //   SearchFilters.IncludePrerelease:
            //     All versions: 1.0.0
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = true
            //
            // In cases #1 and #2, we know the value of IsLatestStable will always be true. We cannot know whether
            // IsLatest is true or false without looking at the version list that includes prerelease versions. For
            // cases #3 and #4, we know IsLatest will always be true and we can determine IsLatestStable by looking
            // at whether the latest version is prerelease or not.
            bool isLatestStable;
            bool isLatest;
            if ((searchFilters & SearchFilters.IncludePrerelease) == 0)
            {
                // This is the case where prerelease versions are excluded.
                var latestIncludePrerelease = versionLists
                    .GetLatestVersionInfoOrNull(searchFilters | SearchFilters.IncludePrerelease);
                Guard.Assert(
                    latestIncludePrerelease != null,
                    "If a search filter excludes prerelease and has a latest version, then there is a latest version including prerelease.");
                isLatestStable = true;
                isLatest = latestIncludePrerelease.ParsedVersion == latest.ParsedVersion;
            }
            else
            {
                // This is the case where prerelease versions are included.
                isLatestStable = !latest.ParsedVersion.IsPrerelease;
                isLatest = true;
            }

            return new SearchDocument.LatestFlags(latest, isLatestStable, isLatest);
        }

        public KeyedDocument Keyed(
            string packageId,
            SearchFilters searchFilters)
        {
            var document = new KeyedDocument();

            PopulateKey(document, packageId, searchFilters);

            return document;
        }

        public SearchDocument.UpdateVersionList UpdateVersionListFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest)
        {
            var document = new SearchDocument.UpdateVersionList();

            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: lastCommitTimestamp,
                lastCommitId: lastCommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest);

            return document;
        }

        public SearchDocument.UpdateVersionListAndOwners UpdateVersionListAndOwnersFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string[] owners)
        {
            var document = new SearchDocument.UpdateVersionListAndOwners();

            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: lastCommitTimestamp,
                lastCommitId: lastCommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest);
            PopulateOwners(
                document,
                owners);

            return document;
        }

        public SearchDocument.UpdateLatest UpdateLatestFromCatalog(
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string normalizedVersion,
            string fullVersion,
            PackageDetailsCatalogLeaf leaf,
            string[] owners)
        {
            var document = new SearchDocument.UpdateLatest();

            // Determine if we have packageTypes to forward.
            // Otherwise, we need to let the system know that there were no explicit package types
            var packageTypes = leaf.PackageTypes != null && leaf.PackageTypes.Count > 0 ?
                leaf.PackageTypes.Select(pt => pt.Name).ToArray() :
                null;

            PopulateUpdateLatest(
                document,
                leaf.PackageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: leaf.CommitTimestamp,
                lastCommitId: leaf.CommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest,
                fullVersion: fullVersion,
                owners: owners,
                packageTypes: packageTypes);
            _baseDocumentBuilder.PopulateMetadata(document, normalizedVersion, leaf);
            PopulateDeprecationFromCatalog(document, leaf);
            PopulateVulnerabilitiesFromCatalog(document, leaf);

            return document;
        }

        public SearchDocument.Full FullFromDb(
            string packageId,
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            Package package,
            string[] owners,
            long totalDownloadCount,
            bool isExcludedByDefault)
        {
            var document = new SearchDocument.Full();

            // Determine if we have packageTypes to forward.
            // Otherwise, we need to let the system know that there were no explicit package types
            var packageTypes = package.PackageTypes != null && package.PackageTypes.Count > 0 ?
                package.PackageTypes.Select(pt => pt.Name).ToArray() :
                null;

            PopulateUpdateLatest(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: false,
                lastCommitTimestamp: null,
                lastCommitId: null,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest,
                fullVersion: fullVersion,
                owners: owners,
                packageTypes: packageTypes);
            _baseDocumentBuilder.PopulateMetadata(document, packageId, package);
            PopulateDownloadCount(document, totalDownloadCount);
            PopulateIsExcludedByDefault(document, isExcludedByDefault);
            PopulateDeprecationFromDb(document, package);
            PopulateVulnerabilitiesFromDb(document, package);

            return document;
        }

        private void PopulateVersions<T>(
            T document,
            string packageId,
            SearchFilters searchFilters,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest) where T : KeyedDocument, SearchDocument.IVersions
        {
            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateCommitted(
                document,
                lastUpdatedFromCatalog,
                lastCommitTimestamp,
                lastCommitId);
            document.Versions = versions;
            document.IsLatestStable = isLatestStable;
            document.IsLatest = isLatest;
        }

        private static void PopulateKey(KeyedDocument document, string packageId, SearchFilters searchFilters)
        {
            document.Key = DocumentUtilities.GetSearchDocumentKey(packageId, searchFilters);
        }

        private void PopulateUpdateLatest(
            SearchDocument.UpdateLatest document,
            string packageId,
            SearchFilters searchFilters,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            string[] owners,
            string[] packageTypes)
        {
            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog,
                lastCommitTimestamp,
                lastCommitId,
                versions,
                isLatestStable,
                isLatest);
            document.SearchFilters = DocumentUtilities.GetSearchFilterString(searchFilters);
            document.FullVersion = fullVersion;

            // If the package has explicit types, we will set them here.
            // Otherwise, we will treat the package as a "Depedency" type and fill in the explicit type.
            if (packageTypes != null && packageTypes.Length > 0)
            {
                document.PackageTypes = packageTypes;
                document.FilterablePackageTypes = packageTypes.Select(pt => pt.ToLowerInvariant()).ToArray();
            }
            else
            {
                document.PackageTypes = ImpliedDependency;
                document.FilterablePackageTypes = FilterableImpliedDependency;
            }

            PopulateOwners(
                document,
                owners);
        }

        private static void PopulateOwners<T>(
            T document,
            string[] owners) where T : KeyedDocument, SearchDocument.IOwners
        {
            document.Owners = owners;
        }

        public SearchDocument.UpdateOwners UpdateOwners(
            string packageId,
            SearchFilters searchFilters,
            string[] owners)
        {
            var document = new SearchDocument.UpdateOwners();

            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateUpdated(
                document,
                lastUpdatedFromCatalog: false);
            PopulateOwners(document, owners);

            return document;
        }

        public SearchDocument.UpdateDownloadCount UpdateDownloadCount(
            string packageId,
            SearchFilters searchFilters,
            long totalDownloadCount)
        {
            var document = new SearchDocument.UpdateDownloadCount();

            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateUpdated(
                document,
                lastUpdatedFromCatalog: false);
            PopulateDownloadCount(document, totalDownloadCount);

            return document;
        }

        private static void PopulateDownloadCount<T>(
            T document,
            long totalDownloadCount) where T : KeyedDocument, SearchDocument.IDownloadCount
        {
            document.TotalDownloadCount = totalDownloadCount;
            document.DownloadScore = DocumentUtilities.GetDownloadScore(totalDownloadCount);
        }

        private static void PopulateIsExcludedByDefault<T>(
            T document,
            bool isExcludedByDefault) where T : KeyedDocument, SearchDocument.IIsExcludedByDefault
        {
            document.IsExcludedByDefault = isExcludedByDefault;
        }

        private static void PopulateDeprecationFromDb(
            SearchDocument.Full document,
            Package package)
        {
            if (package.Deprecations != null && package.Deprecations.Count == 1 && package.Deprecations.ElementAt(0).Status != PackageDeprecationStatus.NotDeprecated)
            {
                document.Deprecation = new Deprecation();

                var packagedeprecation = package.Deprecations.ElementAt(0);
                document.Deprecation.Message = packagedeprecation.CustomMessage;
                document.Deprecation.Reasons = packagedeprecation.Status.ToString().Replace(" ", "").Split(',');

                if (package.Deprecations.ElementAt(0).AlternatePackage != null)
                {
                    document.Deprecation.AlternatePackage = new AlternatePackage();
                    document.Deprecation.AlternatePackage.Id = packagedeprecation.AlternatePackage.Id;
                    var version = packagedeprecation.AlternatePackage.Version ?? "";
                    document.Deprecation.AlternatePackage.Range = "[" + version + ", )";
                }
            }
        }

       private static void PopulateDeprecationFromCatalog(
            SearchDocument.UpdateLatest document,
            PackageDetailsCatalogLeaf leaf)
        {
            if (leaf.Deprecation != null && leaf.Deprecation.Reasons != null && leaf.Deprecation.Reasons.Count > 0)
            {
                document.Deprecation = new Deprecation();
                document.Deprecation.Reasons = new string[leaf.Deprecation.Reasons.Count];

                document.Deprecation.Message = leaf.Deprecation.Message;
                leaf.Deprecation.Reasons.CopyTo(document.Deprecation.Reasons);

                if (leaf.Deprecation.AlternatePackage != null)
                {
                    document.Deprecation.AlternatePackage = new AlternatePackage();
                    document.Deprecation.AlternatePackage.Id = leaf.Deprecation.AlternatePackage.Id;
                    document.Deprecation.AlternatePackage.Range = leaf.Deprecation.AlternatePackage.Range;
                }
            }
        }

        private static void PopulateVulnerabilitiesFromDb(
            SearchDocument.Full document,
            Package package)
        {
            document.Vulnerabilities = new List<Vulnerability>();
            if (package.VulnerablePackageRanges != null && package.VulnerablePackageRanges.Count > 0)
            {
                foreach (VulnerablePackageVersionRange v in package.VulnerablePackageRanges)
                {
                    if (v != null && v.Vulnerability != null)
                    {
                        var vulnerability = new Vulnerability();
                        vulnerability.AdvisoryURL = v.Vulnerability.AdvisoryUrl;
                        vulnerability.Severity = (int)v.Vulnerability.Severity;
                        document.Vulnerabilities.Add(vulnerability);
                    }
                }
            }
        }

        private static void PopulateVulnerabilitiesFromCatalog(
            SearchDocument.UpdateLatest document,
            PackageDetailsCatalogLeaf leaf)
        {
            document.Vulnerabilities = new List<Vulnerability>();
            if (leaf.Vulnerabilities != null && leaf.Vulnerabilities.Count > 0)
            {
                foreach (Protocol.Catalog.PackageVulnerability v in leaf.Vulnerabilities)
                {
                    if (v != null)
                    {
                        var vulnerability = new Vulnerability();
                        vulnerability.AdvisoryURL = v.AdvisoryUrl;

                        PackageVulnerabilitySeverity severity;
                        if (Enum.TryParse<PackageVulnerabilitySeverity>(v.Severity, out severity))
                        {
                            vulnerability.Severity = (int)severity;
                        }
    
                        document.Vulnerabilities.Add(vulnerability);
                    }
                }
            }
        }
    }
}
