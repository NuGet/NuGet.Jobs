// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.SearchService
{
    public static class ParameterUtilities
    {
        private static readonly NuGetVersion SemVer2Level = new NuGetVersion("2.0.0");

        private static readonly IReadOnlyDictionary<string, V2SortBy> SortBy = new Dictionary<string, V2SortBy>(StringComparer.OrdinalIgnoreCase)
        {
            { "relevance", V2SortBy.Popularity },
            { "lastEdited", V2SortBy.LastEditedDesc },
            { "published", V2SortBy.PublishedDesc },
            { "title-asc", V2SortBy.SortableTitleAsc },
            { "title-desc", V2SortBy.SortableTitleDesc },
            { "created-asc", V2SortBy.CreatedAsc },
            { "created-desc", V2SortBy.CreatedDesc },
            { "totalDownloads-asc", V2SortBy.TotalDownloadsAsc },
            { "totalDownloads-desc", V2SortBy.TotalDownloadsDesc },
        };

        private static readonly HashSet<string> FrameworkGenerationIdentifiers = new HashSet<string>{
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                                                                                        AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard
        };

        public static V2SortBy ParseV2SortBy(string sortBy)
        {
            if (sortBy == null || !SortBy.TryGetValue(sortBy, out var parsedSortBy))
            {
                parsedSortBy = V2SortBy.Popularity;
            }

            return parsedSortBy;
        }

        public static bool ParseIncludeSemVer2(string semVerLevel)
        {
            if (!NuGetVersion.TryParse(semVerLevel, out var semVerLevelVersion))
            {
                return false;
            }
            else
            {
                return semVerLevelVersion >= SemVer2Level;
            }
        }

        public static IReadOnlyList<string> ParseFrameworks(string frameworks)
        {
            return frameworks == null ? new List<string>() : frameworks
                                                                .Split(',')
                                                                .Select(f => f.ToLowerInvariant().Trim())
                                                                .Where(f => f != String.Empty)
                                                                .Where(f => FrameworkGenerationIdentifiers.Contains(f))
                                                                .ToList();
        }

        public static IReadOnlyList<string> ParseTfms(string tfms)
        {
            return tfms == null ? new List<string>() : tfms
                                                        .Split(',')
                                                        .Select(f => f.Trim())
                                                        .Where(f => f != String.Empty)
                                                        .Select(f => NuGetFramework.Parse(f))
                                                        .Where(f => f.IsSpecificFramework && !f.IsPCL)
                                                        .Select(f => f.GetShortFolderName())
                                                        .ToList();
        }
    }
}
