﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using NuGet.Services.Configuration;

namespace NuGet.Indexing
{
    /// <summary>
    /// Provides configuration for Indexing.
    /// </summary>
    public class IndexingConfiguration : NuGet.Services.Configuration.Configuration
    {
        private const string SearchPrefix = "Search.";
        private const string LocalPrefix = "Local.";
        private const string StoragePrefix = "Storage.";

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue(60*60)] // 1 hour
        public int AuxiliaryDataRefreshRateSec { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue(60*60)] // 1 hour
        public int IndexReloadRateSec { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("ng-search-data")]
        public string DataContainer { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("ng-search-index")]
        public string IndexContainer { get; set; }

        /// <summary>
        /// The path used to cache the indexes' Azure Directory. This value is overwritten at
        /// runtime if <see cref="AzureDirectoryCacheLocalResourceName"/> is configured.
        /// </summary>
        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("")]
        public string AzureDirectoryCachePath { get; set; }

        /// <summary>
        /// If set, the root path of the Azure Local Storage resource will be used as the
        /// <see cref="AzureDirectoryCachePath"/>.
        /// </summary>
        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("")]
        public string AzureDirectoryCacheLocalResourceName { get; set; }

        [ConfigurationKeyPrefix(LocalPrefix)]
        [ConfigurationKey("Data.Directory")]
        public string LocalDataDirectory { get; set; }

        [ConfigurationKeyPrefix(LocalPrefix)]
        [ConfigurationKey("Lucene.Directory")]
        public string LocalLuceneDirectory { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("http://api.nuget.org/v3/registration1/")]
        public string RegistrationBaseAddress { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [DefaultValue("http://api.nuget.org/v3/registration2-gz-semver2/")]
        public string SemVer2RegistrationBaseAddress { get; set; }

        [ConfigurationKeyPrefix(StoragePrefix)]
        [ConfigurationKey("Primary")]
        public string StoragePrimary { get; set; }
    }
}
