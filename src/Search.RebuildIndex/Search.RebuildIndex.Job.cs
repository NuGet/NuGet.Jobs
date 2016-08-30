﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage;
using NuGet.Indexing;
using Lucene.Net.Store;
using System.IO;
using NuGet.Jobs;
using NuGet.Services.KeyVault;

namespace Search.RebuildIndex
{
    /// <summary>
    /// This job is to rebuild a lucene index out of the package database.
    /// This job only supports creating the lucene index on file system. Need to upload the lucene index to azure using lucene copy.
    /// Direct creation of lucene index on Azure is several magnitudes slower than creating it locally and uploading to Azure.
    /// So, it is deliberately disallowed
    /// </summary>
    internal class Job : JobBase
    {
        private const string DefaultDataContainerName = "ng-search-data";

        /// <summary>
        /// The gallery database or the package database
        /// </summary>
        private SqlConnectionStringBuilder PackageDatabase { get; set; }
        /// <summary>
        /// The storage account in which FrameworksList.FileName can be found
        /// </summary>
        private CloudStorageAccount DataStorageAccount { get; set; }
        /// <summary>
        /// The container in DataStorageAccount in which FrameworksList.FileName can be found. Default is 'ng-search-data'
        /// </summary>
        private string DataContainerName { get; set; }
        private string LocalIndexFolder { get; set; }

        public override async Task<bool> Init(IArgumentsDictionary jobArgsDictionary)
        {
            PackageDatabase =
            new SqlConnectionStringBuilder(await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.PackageDatabase));

            DataStorageAccount =
                CloudStorageAccount.Parse(await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.DataStorageAccount));

            DataContainerName = await jobArgsDictionary.GetOrDefault<string>(JobArgumentNames.DataContainerName);

            if (string.IsNullOrEmpty(DataContainerName))
            {
                DataContainerName = DefaultDataContainerName;
            }

            LocalIndexFolder = await jobArgsDictionary.GetOrThrow<string>(JobArgumentNames.LocalIndexFolder);

            // Initialized successfully, return true
            return true;
        }

        public override Task<bool> Run()
        {
            FrameworksList frameworksList = new StorageFrameworksList(DataStorageAccount, DataContainerName, FrameworksList.FileName);
            Lucene.Net.Store.Directory directory = new SimpleFSDirectory(new DirectoryInfo(LocalIndexFolder));

            PackageIndexing.RebuildIndex(PackageDatabase.ConnectionString, directory, frameworksList, Console.Out);

            return Task.FromResult(true);
        }
    }
}
