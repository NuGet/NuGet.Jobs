﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Indexing;
using NuGet.Jobs;
using NuGet.Services.KeyVault;

namespace Search.UpdateIndex
{
    /// <summary>
    /// This job is to update lucene index out of the package database.
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
        /// <summary>
        /// The container in DataStorageAccount where Lucene Search Index is present.
        /// </summary>
        private string ContainerName { get; set; }

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

            ContainerName = await jobArgsDictionary.GetOrDefault<string>(JobArgumentNames.ContainerName);

            // Initialized successfully, return true
            return true;
        }

        public override Task<bool> Run()
        {
            // Run the task
            UpdateIndexTask task = new UpdateIndexTask
            {
                SqlConnectionString = PackageDatabase.ConnectionString,
                StorageAccount = DataStorageAccount,
                Container = ContainerName,
                DataContainer = DataContainerName,
            };
            task.Execute();
            return Task.FromResult(true);
        }
    }
}
