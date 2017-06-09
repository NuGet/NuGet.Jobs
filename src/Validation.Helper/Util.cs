// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;

namespace NuGet.Jobs.Validation.Helper
{
    internal static class Util
    {
        public static async Task<NuGetPackage> GetPackage(
            string galleryBaseAddress, 
            NuGetV2Feed feed, 
            string packageId, 
            string packageVersion)
        {
            var url = GetPackageUrl(galleryBaseAddress, packageId, packageVersion);
            var package = await GetPackage(feed, url);
            if (package != null)
            {
                return package;
            }
            url = GetPackageFallbackUrl(galleryBaseAddress, packageId, packageVersion);
            return await GetPackage(feed, url);
        }

        private static async Task<NuGetPackage> GetPackage(NuGetV2Feed feed, Uri url)
        {
            return (await feed.GetPackagesAsync(url)).FirstOrDefault();
        }

        public static Uri GetPackageFallbackUrl(string galleryBaseAddress, string packageId, string packageVersion)
        {
            return new Uri($"{galleryBaseAddress}/Packages?" +
                $"$filter=Id eq '{packageId}' and Version eq '{packageVersion}' and true");
        }

        public static Uri GetPackageUrl(string galleryBaseAddress, string packageId, string packageVersion)
        {
            return new Uri($"{galleryBaseAddress}/Packages?" +
                $"$filter=Id eq '{packageId}' and NormalizedVersion eq '{packageVersion}' and true");
        }
    }
}
