// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageDefinition
    {
        private const string _nupkgExtension = ".nupkg";
        private const string _dotSeparator = ".";

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public static PackageDefinition FromRequestUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl) || !requestUrl.EndsWith(_nupkgExtension))
            {
                return null;
            }

            requestUrl = HttpUtility.UrlDecode(requestUrl);

            var urlSegments = requestUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            PackageDefinition packageDefinition;
            var isV3Url = TryGetV3PackageDefinition(urlSegments, out packageDefinition);
            var fileName = urlSegments.Last();

            if (isV3Url)
            {
                return packageDefinition;
            }
            else if (fileName.EndsWith(_nupkgExtension))
            {
                var fileNameSegments = fileName.Substring(0, fileName.Length - _nupkgExtension.Length).Split('.');
                var packageIdSegments = new List<string>();
                var packageVersionSegments = new List<string>();

                int? firstPackageVersionSegment = null;
                for (var i = 0; i < fileNameSegments.Length; i++)
                {
                    var segment = fileNameSegments[i];
                    if (i == 0)
                    {
                        // first segment is always part of package id
                        packageIdSegments.Add(segment);
                        continue;
                    }

                    if (i < fileNameSegments.Length - 4)
                    {
                        // version part can only contain 4 segments maximum
                        packageIdSegments.Add(segment);
                        continue;
                    }

                    int parsedSegement;
                    var isNumericSegment = int.TryParse(segment, out parsedSegement);
                    if ((!isNumericSegment && !firstPackageVersionSegment.HasValue) || (!isNumericSegment && i < firstPackageVersionSegment.Value))
                    {
                        packageIdSegments.Add(segment);
                    }
                    else
                    {
                        if (!firstPackageVersionSegment.HasValue)
                        {
                            firstPackageVersionSegment = i;
                        }
                        packageVersionSegments.Add(segment);
                    }
                }

                packageDefinition = new PackageDefinition();
                packageDefinition.PackageId = string.Join(_dotSeparator, packageIdSegments);
                packageDefinition.PackageVersion = string.Join(_dotSeparator, packageVersionSegments);

                return packageDefinition;
            }
            else return null;
        }

        private static bool TryGetV3PackageDefinition(string[] urlSegments, out PackageDefinition result)
        {
            result = null;
            if (urlSegments.Length < 3)
            {
                return false;
            }

            var maybePackageId = urlSegments[urlSegments.Length - 3];
            var maybePackageVersion = urlSegments[urlSegments.Length - 2];
            var reconstructV3FileName = maybePackageId + "." + maybePackageVersion + _nupkgExtension;
            var fileName = urlSegments.Last();

            var isV3Url = String.Compare(fileName, reconstructV3FileName, StringComparison.OrdinalIgnoreCase) == 0;
            if (isV3Url)
            {
                result = new PackageDefinition()
                {
                    PackageId = maybePackageId,
                    PackageVersion = maybePackageVersion
                };

                return true;
            }

            return false;
        }
    }
}