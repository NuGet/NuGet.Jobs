// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public partial class SearchTextBuilder : ISearchTextBuilder
    {
        private readonly SearchText MatchAllDocumentsIncludingTestData = new SearchText("*", isDefaultSearch: true);

        private static readonly char[] PackageIdSeparators = new[] { '.', '-', '_' };
        private static readonly Regex TokenizePackageIdRegex = new Regex(
            @"((?<=[a-z])(?=[A-Z])|((?<=[0-9])(?=[A-Za-z]))|((?<=[A-Za-z])(?=[0-9]))|[^\w]|_)",
            RegexOptions.None,
            matchTimeout: TimeSpan.FromSeconds(10));

        private static readonly IReadOnlyDictionary<QueryField, string> FieldNames = new Dictionary<QueryField, string>
        {
            { QueryField.Author, IndexFields.Authors },
            { QueryField.Description, IndexFields.Description },
            { QueryField.Id, IndexFields.TokenizedPackageId },
            { QueryField.Owner, IndexFields.Search.Owners },
            { QueryField.PackageId, IndexFields.PackageId },
            { QueryField.Summary, IndexFields.Summary },
            { QueryField.Tag, IndexFields.Tags },
            { QueryField.Title, IndexFields.Title },
            { QueryField.Version, IndexFields.NormalizedVersion },
        };

        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly NuGetQueryParser _parser;

        public SearchTextBuilder(IOptionsSnapshot<SearchServiceConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _parser = new NuGetQueryParser();
        }

        public ParsedQuery ParseV2Search(V2SearchRequest request)
        {
            var query = request.Query;

            // The old V2 search service would treat "id:" queries (~match) in the same way as it did "packageid:" (==match).
            // If "id:" is in the query, replace it.
            if (request.LuceneQuery && !string.IsNullOrEmpty(query) && query.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                query = "packageid:" + query.Substring(3);
            }

            // We must include test data when we are querying the hijack index since the owners field is not
            // available in that index. The hijack index is queried when "ignoreFilter=true". Otherwise, the search
            // index is queried and which means it is possible to filter out test data.
            return GetParsedQuery(query, request.IncludeTestData || request.IgnoreFilter);
        }

        public ParsedQuery ParseV3Search(V3SearchRequest request)
        {
            return GetParsedQuery(request.Query, request.IncludeTestData);
        }

        public SearchText Autocomplete(AutocompleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return GetMatchAllDocuments(request.IncludeTestData);
            }

            // Query package ids. If autocompleting package ids, allow prefix matches.
            var builder = new AzureSearchTextBuilder();

            if (request.Type == AutocompleteRequestType.PackageIds)
            {
                var trimmedQuery = request.Query.Trim();

                builder.AppendTerm(
                    fieldName: IndexFields.PackageId,
                    term: trimmedQuery,
                    prefixSearch: true);

                var pieces = trimmedQuery.Split(PackageIdSeparators);
                foreach (var piece in pieces)
                {
                    if (string.IsNullOrWhiteSpace(piece))
                    {
                        continue;
                    }

                    builder.AppendTerm(
                        fieldName: IndexFields.TokenizedPackageId,
                        term: piece,
                        prefix: TermPrefix.And,
                        prefixSearch: true);
                }

                if (IsId(trimmedQuery))
                {
                    builder.AppendExactMatchPackageIdBoost(trimmedQuery, _options.Value.ExactMatchBoost);
                }
            }
            else
            {
                builder.AppendTerm(
                    fieldName: IndexFields.PackageId,
                    term: request.Query,
                    prefixSearch: false);
            }

            if (!request.IncludeTestData)
            {
                ExcludeTestData(builder);
            }

            return new SearchText(builder.ToString(), isDefaultSearch: false);
        }

        private ParsedQuery GetParsedQuery(string query, bool includeTestData)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new ParsedQuery(new Dictionary<QueryField, HashSet<string>>(), includeTestData);
            }

            var grouping = _parser.ParseQuery(query.Trim(), skipWhiteSpace: true);

            return new ParsedQuery(grouping, includeTestData);
        }

        public SearchText Build(ParsedQuery parsed)
        {
            if (!parsed.Grouping.Any())
            {
                return GetMatchAllDocuments(parsed.IncludeTestData);
            }

            var scopedTerms = parsed.Grouping.Where(g => g.Key != QueryField.Any && g.Key != QueryField.Invalid).ToList();
            var unscopedTerms = parsed.Grouping.Where(g => g.Key == QueryField.Any)
                .Select(g => g.Value)
                .SingleOrDefault()?
                .ToList();

            // Don't bother generating Azure Search text if all terms are scoped to invalid fields.
            var hasUnscopedTerms = unscopedTerms != null && unscopedTerms.Count > 0;
            if (scopedTerms.Count == 0 && !hasUnscopedTerms)
            {
                return GetMatchAllDocuments(parsed.IncludeTestData);
            }

            // Add the terms that are scoped to specific fields.
            var builder = new AzureSearchTextBuilder();
            var requireScopedTerms = hasUnscopedTerms || scopedTerms.Count > 1;

            foreach (var scopedTerm in scopedTerms)
            {
                var fieldName = FieldNames[scopedTerm.Key];
                var values = ProcessFieldValues(scopedTerm.Key, scopedTerm.Value).ToList();

                if (values.Count == 0)
                {
                    // This happens if tags have only delimiters.
                    continue;
                }
                else if (values.Count > 1)
                {
                    builder.AppendScopedTerms(fieldName, values, required: requireScopedTerms);
                }
                else
                {
                    builder.AppendTerm(fieldName, values.First(), prefix: requireScopedTerms ? TermPrefix.And : TermPrefix.None);
                }
            }

            // Add the terms that can match any fields.
            if (hasUnscopedTerms)
            {
                // All but the last unscoped tokens must match some part of tokenized package metadata. This ensures
                // that a term that the user adds to their search text is effective. In general, if tokens are optional,
                // any score boost on, say, download count can be highly popular but largely irrelevant packages at the
                // top. For the last token, allow a prefix match to support instant search scenarios.
                var camelSplitTokens = unscopedTerms.SelectMany(TokenizeWithCamelSplit).ToList();
                var uniqueCamelSplitTokens = new HashSet<string>(camelSplitTokens);
                foreach (var token in uniqueCamelSplitTokens)
                {
                    var isLastToken = token == camelSplitTokens.Last();
                    if (isLastToken)
                    {
                        var boost = token.Length < 4 ? _options.Value.PrefixMatchBoost : 1;

                        builder.AppendTerm(
                            fieldName: null,
                            term: token,
                            prefix: TermPrefix.And,
                            prefixSearch: true,
                            boost: boost);
                        builder.AppendTerm(
                            fieldName: null,
                            term: token);
                    }
                    else
                    {
                        builder.AppendTerm(
                            fieldName: null,
                            term: token,
                            prefix: TermPrefix.And);
                    }
                }

                // Favor tokens that match without camel-case split.
                var separatorTokens = new HashSet<string>(unscopedTerms
                    .SelectMany(TokenizeWithSeparators)
                    .Except(uniqueCamelSplitTokens, StringComparer.OrdinalIgnoreCase)
                    .Where(x => x.Length > 3));
                foreach (var term in separatorTokens)
                {
                    builder.AppendTerm(
                        fieldName: null,
                        term: term,
                        prefix: TermPrefix.None,
                        boost: 2);
                }
            }

            // Handle the exact match case. If the search query is a single unscoped term is also a valid package
            // ID, mega boost the document that has this package ID.
            if (scopedTerms.Count == 0
                && unscopedTerms.Count == 1
                && IsIdWithSeparator(unscopedTerms[0]))
            {
                builder.AppendExactMatchPackageIdBoost(unscopedTerms[0], _options.Value.ExactMatchBoost);
            }

            if (!parsed.IncludeTestData)
            {
                ExcludeTestData(builder);
            }

            var result = builder.ToString();

            if (string.IsNullOrWhiteSpace(result))
            {
                return GetMatchAllDocuments(parsed.IncludeTestData);
            }

            return new SearchText(result, isDefaultSearch: false);
        }

        private static IEnumerable<string> ProcessFieldValues(QueryField field, IEnumerable<string> values)
        {
            switch (field)
            {
                // Expand tags by their delimiters
                case QueryField.Tag:
                    return values.SelectMany(Utils.SplitTags).Distinct();

                // The "version" query field should be normalized if possible.
                case QueryField.Version:
                    return values.Select(value =>
                    {
                        if (!NuGetVersion.TryParse(value, out var version))
                        {
                            return value;
                        }

                        return version.ToNormalizedString();
                    });

                default:
                    return values;
            }
        }

        private static bool IsId(string query)
        {
            return query.Length <= PackageIdValidator.MaxPackageIdLength
                && PackageIdValidator.IsValidPackageId(query);
        }

        private static bool IsIdWithSeparator(string query)
        {
            return query.IndexOfAny(PackageIdSeparators) >= 0 && IsId(query);
        }

        /// <summary>
        /// Tokenizes terms. This is similar to <see cref="PackageIdCustomAnalyzer"/> with the following differences:
        /// 
        /// 1. Does not split terms on whitespace
        /// 2. Does not split terms on the following characters: ' ; : * # ! ~ + ( ) [ ] { }
        /// </summary>
        /// <param name="term">The input to tokenize</param>
        /// <returns>The tokens extracted from the inputted term</returns>
        private static IReadOnlyList<string> TokenizeWithCamelSplit(string term)
        {
            // Don't tokenize phrases. These are multiple terms that were wrapped in quotes.
            if (term.Any(char.IsWhiteSpace))
            {
                return new List<string> { term };
            }

            return TokenizePackageIdRegex
                .Split(term)
                .Where(t => !string.IsNullOrEmpty(t))
                .Where(t => t.Length > 1 || char.IsLetterOrDigit(t[0]))
                .ToList();
        }

        /// <summary>
        /// Tokenizes terms. This is similar to <see cref="PackageIdCustomAnalyzer"/> with the following differences:
        /// 
        /// 1. Does not split terms on whitespace
        /// 2. Does not split terms on the following characters: ' ; : * # ! ~ + ( ) [ ] { }
        /// </summary>
        /// <param name="term">The input to tokenize</param>
        /// <returns>The tokens extracted from the inputted term</returns>
        private static IReadOnlyList<string> TokenizeWithSeparators(string term)
        {
            // Don't tokenize phrases. These are multiple terms that were wrapped in quotes.
            if (term.Any(char.IsWhiteSpace))
            {
                return new List<string> { term };
            }

            return term
                .Split(PackageIdSeparators)
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t.ToLowerInvariant())
                .ToList();
        }

        private void ExcludeTestData(AzureSearchTextBuilder builder)
        {
            if (_options.Value.TestOwners != null)
            {
                foreach (var owner in _options.Value.TestOwners)
                {
                    builder.AppendTerm(IndexFields.Search.Owners, owner, prefix: TermPrefix.Not);
                }
            }
        }

        private SearchText GetMatchAllDocuments(bool includeTestData)
        {
            if (includeTestData
                || _options.Value.TestOwners == null
                || _options.Value.TestOwners.Count == 0)
            {
                return MatchAllDocumentsIncludingTestData;
            }

            var builder = new AzureSearchTextBuilder();

            // We can't use '*' to match all documents here since it doesn't work in conjunction with any other terms.
            // Instead, we match all documents by finding every doument that has a package ID (which is all documents).
            builder.AppendVerbatim($"{IndexFields.PackageId}:/.*/");

            ExcludeTestData(builder);

            return new SearchText(builder.ToString(), isDefaultSearch: true);
        }
    }
}
