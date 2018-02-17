// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet.Versioning;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageDefinition
    {
        private const string _nupkgExtension = ".nupkg";
        private const string _dotSeparator = ".";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public static IList<PackageDefinition> FromRequestUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || !requestUrl.EndsWith(_nupkgExtension))
            {
                return null;
            }

            List<PackageDefinition> resolutionOptions = new List<PackageDefinition>();

            requestUrl = HttpUtility.UrlDecode(requestUrl);

            var urlSegments = requestUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var fileName = urlSegments.Last();

            if (fileName.EndsWith(_nupkgExtension))
            {
                fileName = fileName.Remove(fileName.Length - _nupkgExtension.Length, _nupkgExtension.Length);

                var nextDotIndex = fileName.IndexOf('.');

                while (nextDotIndex != -1)
                {
                    string packagePart = fileName.Substring(0, nextDotIndex);
                    string versionPart = fileName.Substring(nextDotIndex + 1);

                    if (NuGetVersion.TryParse(versionPart, out var parsedVersion))
                    {
                        var normalizedVersion = parsedVersion.ToNormalizedString();

                        if (string.Compare(normalizedVersion, versionPart, ignoreCase: true) == 0)
                        {
                            resolutionOptions.Add(new PackageDefinition()
                            {
                                PackageId = packagePart.Trim(),
                                PackageVersion = versionPart.Trim()
                            });
                        }
                    }

                    nextDotIndex = fileName.IndexOf('.', nextDotIndex + 1);
                }
            }

            return resolutionOptions;
        }

        public override string ToString()
        {
            return $"[{PackageId}, {PackageVersion}]";
        }

        private static bool IsNumeric(string segment)
        {
            return int.TryParse(segment, out _);
        }
    }
}