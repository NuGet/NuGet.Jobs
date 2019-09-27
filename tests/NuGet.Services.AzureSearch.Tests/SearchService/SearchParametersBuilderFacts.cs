﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Moq;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchParametersBuilderFacts
    {
        public class GetSearchFilters : BaseFacts
        {
            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, SearchFilters filter)
            {
                var request = new SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var actual = _target.GetSearchFilters(request);

                Assert.Equal(filter, actual);
            }
        }

        public class LastCommitTimestamp : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var output = _target.LastCommitTimestamp();

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.False(output.IncludeTotalResultCount);
                Assert.Equal(new[] { "lastCommitTimestamp desc" }, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
                Assert.Null(output.Filter);
            }
        }

        public class V2Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V2SearchRequest();

                var output = _target.V2Search(request, isDefaultSearch: true);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
            }

            [Fact]
            public void CountOnly()
            {
                var request = new V2SearchRequest
                {
                    CountOnly = true,
                    Skip = 10,
                    Take = 30,
                    SortBy = V2SortBy.SortableTitleAsc,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
            }

            [Fact]
            public void Paging()
            {
                var request = new V2SearchRequest
                {
                    Skip = 10,
                    Take = 30,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V2SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Theory]
            [InlineData(false, false, "semVerLevel ne 2")]
            [InlineData(true, false, "semVerLevel ne 2")]
            [InlineData(false, true, null)]
            [InlineData(true, true, null)]
            public void IgnoreFilter(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V2SearchRequest
                {
                    IgnoreFilter = true,
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }

            [Theory]
            [MemberData(nameof(AllV2SortBy))]
            public void SortBy(V2SortBy v2SortBy)
            {
                var request = new V2SearchRequest
                {
                    SortBy = v2SortBy,
                };
                var expectedOrderBy = V2SortByToOrderBy[v2SortBy];

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.NotNull(output.OrderBy);
                Assert.Equal(expectedOrderBy, output.OrderBy.ToArray());
            }

            [Theory]
            [MemberData(nameof(AllV2SortBy))]
            public void AllSortByFieldsAreSortable(V2SortBy v2SortBy)
            {
                var metadataProperties = typeof(BaseMetadataDocument)
                    .GetProperties()
                    .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
                var expectedOrderBy = V2SortByToOrderBy[v2SortBy];

                foreach (var clause in expectedOrderBy)
                {
                    var pieces = clause.Split(new[] { ' ' }, 2);
                    Assert.Equal(2, pieces.Length);
                    Assert.Contains(pieces[1], new[] { "asc", "desc" });

                    // This is a magic property name that refers to the document's score, not a particular property.
                    if (pieces[0] == "search.score()")
                    {
                        continue;
                    }

                    Assert.Contains(pieces[0], metadataProperties.Keys);
                    var property = metadataProperties[pieces[0]];
                    var customAttributeTypes = property
                        .CustomAttributes
                        .Select(x => x.AttributeType)
                        .ToArray();
                    Assert.Contains(typeof(IsSortableAttribute), customAttributeTypes);
                }
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V2SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }
        }

        public class V3Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V3SearchRequest();

                var output = _target.V3Search(request, isDefaultSearch: true);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
            }

            [Fact]
            public void Paging()
            {
                var request = new V3SearchRequest
                {
                    Skip = 10,
                    Take = 30,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V3SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V3SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }
        }

        public class Autocomplete : BaseFacts
        {
            [Fact]
            public void PackageIdsDefaults()
            {
                var request = new AutocompleteRequest();
                request.Type = AutocompleteRequestType.PackageIds;

                var output = _target.Autocomplete(request, isDefaultSearch: true);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
                Assert.Single(output.Select);
                Assert.Equal(IndexFields.PackageId, output.Select[0]);
            }

            [Fact]
            public void PackageVersionsDefaults()
            {
                var request = new AutocompleteRequest();
                request.Type = AutocompleteRequestType.PackageVersions;

                var output = _target.Autocomplete(request, isDefaultSearch: true);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
                Assert.Single(output.Select);
                Assert.Equal(IndexFields.Search.Versions, output.Select[0]);
            }

            [Fact]
            public void Paging()
            {
                var request = new AutocompleteRequest
                {
                    Skip = 10,
                    Take = 30,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
            }

            [Fact]
            public void PackageVersionsPaging()
            {
                var request = new AutocompleteRequest
                {
                    Skip = 10,
                    Take = 30,
                    Type = AutocompleteRequestType.PackageVersions,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new AutocompleteRequest
                {
                    Skip = -10,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new AutocompleteRequest
                {
                    Take = -20,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new AutocompleteRequest
                {
                    Type = AutocompleteRequestType.PackageIds,
                    Take = 1001,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(20, output.Top);
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new AutocompleteRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly SearchParametersBuilder _target;

            public static string[] DefaultOrderBy => new[] { "search.score() desc", "created desc" };

            public static IReadOnlyDictionary<V2SortBy, string[]> V2SortByToOrderBy => new Dictionary<V2SortBy, string[]>
            {
                { V2SortBy.LastEditedDesc, new[] { "lastEdited desc", "created desc" } },
                { V2SortBy.Popularity, DefaultOrderBy },
                { V2SortBy.PublishedDesc, new[] { "published desc", "created desc" } },
                { V2SortBy.SortableTitleAsc, new[] { "sortableTitle asc", "created asc" } },
                { V2SortBy.SortableTitleDesc, new[] { "sortableTitle desc", "created desc" } },
                { V2SortBy.CreatedAsc, new[] { "created asc" } },
                { V2SortBy.CreatedDesc, new[] { "created desc" } },
            };

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { false, false, SearchFilters.Default },
                new object[] { true, false, SearchFilters.IncludePrerelease },
                new object[] { false, true, SearchFilters.IncludeSemVer2 },
                new object[] { true, true, SearchFilters.IncludePrereleaseAndSemVer2 },
            };

            public static IEnumerable<object[]> AllSearchFiltersExpressions => AllSearchFilters
                .Select(x => new[] { x[0], x[1], $"searchFilters eq '{x[2]}'" });

            public static IEnumerable<object[]> AllV2SortBy => Enum
                .GetValues(typeof(V2SortBy))
                .Cast<V2SortBy>()
                .Select(x => new object[] { x });

            public BaseFacts()
            {
                _target = new SearchParametersBuilder();
            }
        }
    }
}
