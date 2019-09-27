﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilderFacts
    {
        public class LatestFlagsOrNull : BaseFacts
        {
            public LatestFlagsOrNull(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludeSemVer2)]
            public void ExcludePrereleaseWithOnlyOnePrereleaseVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Null(actual);
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludePrerelease)]
            public void ExcludingSemVer2WithOnlySemVer2(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0+git", "2.0.0-alpha.1");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Null(actual);
            }

            [Theory]
            [InlineData(SearchFilters.IncludePrerelease)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2)]
            public void IncludePrereleaseWithOnlyOnePrereleaseVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal("1.0.0-alpha", actual.LatestVersionInfo.FullVersion);
                Assert.False(actual.IsLatestStable);
                Assert.True(actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludePrerelease)]
            [InlineData(SearchFilters.IncludeSemVer2)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2)]
            public void OnlyOneStableVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal("1.0.0", actual.LatestVersionInfo.FullVersion);
                Assert.True(actual.IsLatestStable);
                Assert.True(actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludeSemVer2, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludePrerelease, "2.0.0-alpha", false, true)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, "2.0.0-alpha", false, true)]
            public void LatestIsPrereleaseWithLowerStable(SearchFilters searchFilters, string latest, bool isLatestStable, bool isLatest)
            {
                var versionLists = VersionLists("1.0.0", "2.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal(latest, actual.LatestVersionInfo.FullVersion);
                Assert.Equal(isLatestStable, actual.IsLatestStable);
                Assert.Equal(isLatest, actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludePrerelease, "2.0.0-alpha", false, true)]
            [InlineData(SearchFilters.IncludeSemVer2, "3.0.0+git", true, false)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, "4.0.0-beta.1", false, true)]
            public void AllVersionTypes(SearchFilters searchFilters, string latest, bool isLatestStable, bool isLatest)
            {
                var versionLists = VersionLists("1.0.0", "2.0.0-alpha", "3.0.0+git", "4.0.0-beta.1");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal(latest, actual.LatestVersionInfo.FullVersion);
                Assert.Equal(isLatestStable, actual.IsLatestStable);
                Assert.Equal(isLatest, actual.IsLatest);
            }

            private static VersionLists VersionLists(params string[] versions)
            {
                return new VersionLists(new VersionListData(versions
                    .Select(x => NuGetVersion.Parse(x))
                    .ToDictionary(x => x.ToFullString(), x => new VersionPropertiesData(listed: true, semVer2: x.IsSemVer2))));
            }
        }

        public class Keyed : BaseFacts
        {
            public Keyed(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Keyed(Data.PackageId, Data.SearchFilters);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateOwners : BaseFacts
        {
            public UpdateOwners(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.UpdateOwners(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+UpdateOwners"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateDownloadCount : BaseFacts
        {
            public UpdateDownloadCount(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.UpdateDownloadCount(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.TotalDownloadCount);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""downloadScore"": 0.14381174563233068,
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+UpdateDownloadCount"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateVersionListFromCatalog : BaseFacts
        {
            public UpdateVersionListFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task SetsExpectedProperties(bool isLatestStable, bool isLatest)
            {
                var document = _target.UpdateVersionListFromCatalog(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.CommitTimestamp,
                    Data.CommitId,
                    Data.Versions,
                    isLatestStable,
                    isLatest);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""isLatestStable"": " + isLatestStable.ToString().ToLowerInvariant() + @",
      ""isLatest"": " + isLatest.ToString().ToLowerInvariant() + @",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00+00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+UpdateVersionList"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateVersionListAndOwnersFromCatalog : BaseFacts
        {
            public UpdateVersionListAndOwnersFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task SetsExpectedProperties(bool isLatestStable, bool isLatest)
            {
                var document = _target.UpdateVersionListAndOwnersFromCatalog(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.CommitTimestamp,
                    Data.CommitId,
                    Data.Versions,
                    isLatestStable,
                    isLatest,
                    Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""isLatestStable"": " + isLatestStable.ToString().ToLowerInvariant() + @",
      ""isLatest"": " + isLatest.ToString().ToLowerInvariant() + @",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00+00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+UpdateVersionListAndOwners"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateLatestFromCatalog : BaseFacts
        {
            public UpdateLatestFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForTitle(string title)
            {
                var leaf = Data.Leaf;
                leaf.Title = title;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.PackageId, document.Title);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesLowerIdWhenMissingForSortableTitle(string title)
            {
                var leaf = Data.Leaf;
                leaf.Title = title;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.PackageId.ToLowerInvariant(), document.SortableTitle);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.UpdateLatestFromCatalog(
                    searchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: Data.Leaf,
                    owners: Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""searchFilters"": """ + expected + @""",
      ""fullVersion"": ""7.1.2-alpha+git"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""isLatestStable"": false,
      ""isLatest"": true,
      ""semVerLevel"": 2,
      ""authors"": ""Microsoft"",
      ""copyright"": ""© Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00+00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""lastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha+git"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""sortableTitle"": ""windows azure storage"",
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""tokenizedPackageId"": ""WindowsAzure.Storage"",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00+00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+UpdateLatest"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }

            [Fact]
            public void LeavesNullRequiresLicenseAcceptanceAsNull()
            {
                var leaf = Data.Leaf;
                leaf.RequireLicenseAgreement = null;                

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Null(document.RequiresLicenseAcceptance);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseExpression()
            {
                var leaf = Data.Leaf;
                leaf.LicenseExpression = "MIT";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseFile()
            {
                var leaf = Data.Leaf;
                leaf.LicenseFile = "LICENSE.txt";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Fact]
            public void SetsIconUrlToFlatContainerWhenPackageHasIconFileAndIconUrl()
            {
                var leaf = Data.Leaf;
                leaf.IconUrl = "https://other-example/icon.png";
                leaf.IconFile = "icon.png";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.FlatContainerIconUrl, document.IconUrl);
            }

            [Fact]
            public void SetsIconUrlToFlatContainerWhenPackageHasIconFileAndNoIconUrl()
            {
                var leaf = Data.Leaf;
                leaf.IconUrl = null;
                leaf.IconFile = "icon.png";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.FlatContainerIconUrl, document.IconUrl);
            }
        }

        public class FullFromDb : BaseFacts
        {
            public FullFromDb(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void NormalizesSortableTitle()
            {
                var package = Data.PackageEntity;
                package.Title = "  Some Title ";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal("some title", document.SortableTitle);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForTitle(string title)
            {
                var package = Data.PackageEntity;
                package.Title = title;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.PackageId, document.Title);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesLowerIdWhenMissingForSortableTitle(string title)
            {
                var package = Data.PackageEntity;
                package.Title = title;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.PackageId.ToLowerInvariant(), document.SortableTitle);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void SetsIsExcludedByDefaultPropertyCorrectly(bool shouldBeExcluded)
            {
                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: Data.PackageEntity,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: shouldBeExcluded);

                Assert.Equal(shouldBeExcluded, document.IsExcludedByDefault);
            }

            [Fact]
            public async Task SerializesNullSemVerLevel()
            {
                var package = Data.PackageEntity;
                package.SemVerLevelKey = SemVerLevelKey.Unknown;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Contains("\"semVerLevel\": null,", json);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.FullFromDb(
                    Data.PackageId,
                    searchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: Data.PackageEntity,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""downloadScore"": 0.14381174563233068,
      ""isExcludedByDefault"": false,
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""searchFilters"": """ + expected + @""",
      ""fullVersion"": ""7.1.2-alpha+git"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""isLatestStable"": false,
      ""isLatest"": true,
      ""semVerLevel"": 2,
      ""authors"": ""Microsoft"",
      ""copyright"": ""© Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00+00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""lastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha+git"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""sortableTitle"": ""windows azure storage"",
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""tokenizedPackageId"": ""WindowsAzure.Storage"",
      ""lastCommitTimestamp"": null,
      ""lastCommitId"": null,
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00+00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument+Full"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }

            [Fact]
            public void SplitsTags()
            {
                var package = Data.PackageEntity;
                package.Tags = "foo; BAR |     Baz";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(new[] { "foo", "BAR", "Baz" }, document.Tags);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseExpression()
            {
                var package = Data.PackageEntity;
                package.LicenseExpression = "MIT";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseFile(EmbeddedLicenseFileType type)
            {
                var package = Data.PackageEntity;
                package.EmbeddedLicenseType = type;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly ITestOutputHelper _output;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly BaseDocumentBuilder _baseDocumentBuilder;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly SearchDocumentBuilder _target;

            public static IEnumerable<object[]> MissingTitles = new[]
            {
                new object[] { null },
                new object[] { string.Empty },
                new object[] { " " },
                new object[] { " \t"},
            };

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { SearchFilters.Default, "Default" },
                new object[] { SearchFilters.IncludePrerelease, "IncludePrerelease" },
                new object[] { SearchFilters.IncludeSemVer2, "IncludeSemVer2" },
                new object[] { SearchFilters.IncludePrereleaseAndSemVer2, "IncludePrereleaseAndSemVer2" },
            };

            [Fact]
            public void AllSearchFiltersAreCovered()
            {
                var testedSearchFilters = AllSearchFilters.Select(x => (SearchFilters)x[0]).ToList();
                var allSearchFilters = Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>().ToList();

                Assert.Empty(testedSearchFilters.Except(allSearchFilters));
                Assert.Empty(allSearchFilters.Except(testedSearchFilters));
            }

            public void SetDocumentLastUpdated(IUpdatedDocument document)
            {
                Data.SetDocumentLastUpdated(document, _output);
            }

            public BaseFacts(ITestOutputHelper output)
            {
                _output = output;
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _baseDocumentBuilder = new BaseDocumentBuilder(_options.Object); // We intentionally don't mock this.
                _config = new AzureSearchJobConfiguration
                {
                    GalleryBaseUrl = Data.GalleryBaseUrl,
                    FlatContainerBaseUrl = Data.FlatContainerBaseUrl,
                    FlatContainerContainerName = Data.FlatContainerContainerName,
                };

                _options.Setup(o => o.Value).Returns(() => _config);

                _target = new SearchDocumentBuilder(_baseDocumentBuilder);
            }
        }
    }
}
