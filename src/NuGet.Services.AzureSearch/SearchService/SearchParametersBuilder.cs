﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchParametersBuilder : ISearchParametersBuilder
    {
        public const int DefaultTake = 20;
        private const int MaximumTake = 1000;
        private const string Score = "search.score()";
        private const string Asc = " asc";
        private const string Desc = " desc";

        private static readonly List<string> LastCommitTimestampSelect = new List<string> { IndexFields.LastCommitTimestamp };
        private static readonly List<string> PackageIdsAutocompleteSelect = new List<string> { IndexFields.PackageId };
        private static readonly List<string> PackageVersionsAutocompleteSelect = new List<string> { IndexFields.Search.Versions };

        private static readonly List<string> LastCommitTimestampDescending = new List<string> { IndexFields.LastCommitTimestamp + Desc }; // Most recently added to the catalog first

        /// <summary>
        /// We use the created timestamp as a tie-breaker since it does not change for a given package.
        /// See: https://stackoverflow.com/a/34234258/52749
        /// </summary>
        private static readonly List<string> ScoreDesc = new List<string> { Score + Desc, IndexFields.Created + Desc }; // Highest score first ("most relevant"), then newest
        private static readonly List<string> LastEditedDesc = new List<string> { IndexFields.LastEdited + Desc, IndexFields.Created + Desc }; // Most recently edited first, then newest
        private static readonly List<string> PublishedDesc = new List<string> { IndexFields.Published + Desc, IndexFields.Created + Desc }; // Most recently published first, then newest
        private static readonly List<string> SortableTitleAsc = new List<string> { IndexFields.SortableTitle + Asc, IndexFields.Created + Asc }; // First title by lex order first, then oldest
        private static readonly List<string> SortableTitleDesc = new List<string> { IndexFields.SortableTitle + Desc, IndexFields.Created + Desc }; // Last title by lex order first, then newest
        private static readonly List<string> CreatedAsc = new List<string> { IndexFields.Created + Asc }; // Newest first
        private static readonly List<string> CreatedDesc = new List<string> { IndexFields.Created + Desc }; // Oldest first
        private static readonly List<string> TotalDownloadsAsc = new List<string> { IndexFields.Search.TotalDownloadCount + Asc, IndexFields.Created + Asc }; // Least downloads first, then oldest
        private static readonly List<string> TotalDownloadsDesc = new List<string> { IndexFields.Search.TotalDownloadCount + Desc, IndexFields.Created + Desc }; // Most downloads first, then newest

        public SearchOptions LastCommitTimestamp()
        {
            var options = new SearchOptions
            {
                QueryType = SearchQueryType.Full,
                Skip = 0,
                Size = 1,
            };
            options.Select.AddRange(LastCommitTimestampSelect);
            options.OrderBy.AddRange(LastCommitTimestampDescending);
            return options;
        }

        public SearchOptions V2Search(V2SearchRequest request, bool isDefaultSearch)
        {
            var searchParameters = new SearchOptions
            {
                IncludeTotalCount = true,
                QueryType = SearchQueryType.Full,
            };

            if (request.CountOnly)
            {
                searchParameters.Skip = 0;
                searchParameters.Size = 0;
                searchParameters.OrderBy.Clear();
            }
            else
            {
                ApplyPaging(searchParameters, request);
                searchParameters.OrderBy.Clear();
                searchParameters.OrderBy.AddRange(GetOrderBy(request.SortBy));
            }

            if (request.IgnoreFilter)
            {
                // Note that the prerelease flag has no effect when IgnoreFilter is true.

                if (!request.IncludeSemVer2)
                {
                    searchParameters.Filter = $"{IndexFields.SemVerLevel} ne {SemVerLevelKey.SemVer2}";
                }
            }
            else
            {
                ApplySearchIndexFilter(searchParameters, request, isDefaultSearch, request.PackageType, request.Frameworks, request.Tfms);
            }

            return searchParameters;
        }

        public SearchOptions V3Search(V3SearchRequest request, bool isDefaultSearch)
        {
            var searchParameters = new SearchOptions
            {
                IncludeTotalCount = true,
                QueryType = SearchQueryType.Full,
            };
            searchParameters.OrderBy.AddRange(ScoreDesc);

            ApplyPaging(searchParameters, request);
            ApplySearchIndexFilter(searchParameters, request, isDefaultSearch, request.PackageType);

            return searchParameters;
        }

        public SearchOptions Autocomplete(AutocompleteRequest request, bool isDefaultSearch)
        {
            var searchParameters = new SearchOptions
            {
                IncludeTotalCount = true,
                QueryType = SearchQueryType.Full,
            };
            searchParameters.OrderBy.AddRange(ScoreDesc);

            ApplySearchIndexFilter(searchParameters, request, isDefaultSearch, request.PackageType);

            switch (request.Type)
            {
                case AutocompleteRequestType.PackageIds:
                    searchParameters.Select.AddRange(PackageIdsAutocompleteSelect);
                    ApplyPaging(searchParameters, request);
                    break;

                // Package version autocomplete should only match a single document
                // regardless of the request's parameters.
                case AutocompleteRequestType.PackageVersions:
                    searchParameters.Select.AddRange(PackageVersionsAutocompleteSelect);
                    searchParameters.Skip = 0;
                    searchParameters.Size = 1;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown autocomplete request type '{request.Type}'");
            }

            return searchParameters;
        }

        private static void ApplyPaging(SearchOptions searchParameters, SearchRequest request)
        {
            searchParameters.Skip = request.Skip < 0 ? 0 : request.Skip;
            searchParameters.Size = request.Take < 0 || request.Take > MaximumTake ? DefaultTake : request.Take;
        }

        private void ApplySearchIndexFilter(
            SearchOptions searchParameters,
            SearchRequest request,
            bool isDefaultSearch,
            string packageType,
            IReadOnlyList<string> frameworks = default,
            IReadOnlyList<string> tfms = default)
        {
            var searchFilters = GetSearchFilters(request);

            var filterString = $"{IndexFields.Search.SearchFilters} eq '{DocumentUtilities.GetSearchFilterString(searchFilters)}'";

            if (isDefaultSearch)
            {
                filterString += $" and ({IndexFields.Search.IsExcludedByDefault} eq false or {IndexFields.Search.IsExcludedByDefault} eq null)";
            }

            // Verify that the package type only has valid package ID characters so we don't need to worry about
            // escaping quotes and such.
            if (packageType != null && PackageIdValidator.IsValidPackageId(packageType))
            {
                filterString += $" and {IndexFields.Search.FilterablePackageTypes}/any(p: p eq '{packageType.ToLowerInvariant()}')";
            }

            if (frameworks != default)
            {
                filterString += GetFrameworksOrTfmsFilterString(IndexFields.Search.Frameworks, frameworks);
            }

            if (tfms != default)
            {
                filterString += GetFrameworksOrTfmsFilterString(IndexFields.Search.Tfms, tfms);
            }

            searchParameters.Filter = filterString;
        }

        public SearchFilters GetSearchFilters(SearchRequest request)
        {
            var searchFilters = SearchFilters.Default;

            if (request.IncludePrerelease)
            {
                searchFilters |= SearchFilters.IncludePrerelease;
            }

            if (request.IncludeSemVer2)
            {
                searchFilters |= SearchFilters.IncludeSemVer2;
            }

            return searchFilters;
        }

        private static IList<string> GetOrderBy(V2SortBy sortBy)
        {
            IList<string> orderBy;
            switch (sortBy)
            {
                case V2SortBy.Popularity:
                    orderBy = ScoreDesc;
                    break;
                case V2SortBy.LastEditedDesc:
                    orderBy = LastEditedDesc;
                    break;
                case V2SortBy.PublishedDesc:
                    orderBy = PublishedDesc;
                    break;
                case V2SortBy.SortableTitleAsc:
                    orderBy = SortableTitleAsc;
                    break;
                case V2SortBy.SortableTitleDesc:
                    orderBy = SortableTitleDesc;
                    break;
                case V2SortBy.CreatedAsc:
                    orderBy = CreatedAsc;
                    break;
                case V2SortBy.CreatedDesc:
                    orderBy = CreatedDesc;
                    break;
                case V2SortBy.TotalDownloadsAsc:
                    orderBy = TotalDownloadsAsc;
                    break;
                case V2SortBy.TotalDownloadsDesc:
                    orderBy = TotalDownloadsDesc;
                    break;
                default:
                    throw new ArgumentException($"The provided {nameof(V2SortBy)} is not supported.", nameof(sortBy));
            }

            return orderBy;
        }

        // Constructs filter strings for both Frameworks and Tfms.
        // indexField: Determines which field you are targeting
        //             i.e. IndexFields.Search.Frameworks or IndexFields.Search.Tfms
        // frameworks: Comma-separated list of a user's selected Frameworks or Tfms
        private string GetFrameworksOrTfmsFilterString(string indexField, IReadOnlyList<string> frameworks)
        {
            var filterStrings = frameworks
                                    .Where(f => IsValidFrameworkOrTfm(indexField, f))
                                    .Select(f => $"{indexField}/any(f: f eq '{f.ToLowerInvariant()}')");

            return filterStrings.Count() == 0
                                ? String.Empty
                                : " and (" + String.Join(" and ", filterStrings) + ")";
        }

        private bool IsValidFrameworkOrTfm(string indexField, string framework)
        {
            if (indexField == IndexFields.Search.Frameworks)
            {
                if (framework == AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net ||
                    framework == AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework ||
                    framework == AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp ||
                    framework == AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard)
                {
                    return true;
                }
            }
            else if (indexField == IndexFields.Search.Tfms)
            {
                var tfm = new PackageFramework() { TargetFramework = framework };
                if (tfm.FrameworkName.IsSpecificFramework && !tfm.FrameworkName.IsPCL)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
