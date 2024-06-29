// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    /// <summary>
    /// The information required to bring an entire package registration up to date in the Azure Search indexes. This
    /// data is populated from the database and storage by db2azuresearch.
    /// </summary>
    public class NewPackageRegistration
    {
        public NewPackageRegistration(
            string packageId,
            long totalDownloadCount,
            string[] owners,
            IReadOnlyList<Package> packages,
            bool isExcludedByDefault) : this(
                packageId,
                totalDownloadCount,
                owners,
                packages,
                versionToReadme: new Dictionary<NuGetVersion, string>(),
                isExcludedByDefault: isExcludedByDefault)
        {
        }

        public NewPackageRegistration(
            string packageId,
            long totalDownloadCount,
            string[] owners,
            IReadOnlyList<Package> packages,
            IReadOnlyDictionary<NuGetVersion, string> versionToReadme,
            bool isExcludedByDefault)
        {
            PackageId = packageId ?? throw new ArgumentNullException(packageId);
            TotalDownloadCount = totalDownloadCount;
            Owners = owners ?? throw new ArgumentNullException(nameof(owners));
            Packages = packages ?? throw new ArgumentNullException(nameof(packages));
            VersionToReadme = versionToReadme ?? throw new ArgumentNullException(nameof(versionToReadme));
            IsExcludedByDefault = isExcludedByDefault;
        }

        public string PackageId { get; }
        public long TotalDownloadCount { get; }
        public string[] Owners { get; }
        public IReadOnlyList<Package> Packages { get; }
        public IReadOnlyDictionary<NuGetVersion, string> VersionToReadme { get; }
        public bool IsExcludedByDefault { get; }
    }
}
