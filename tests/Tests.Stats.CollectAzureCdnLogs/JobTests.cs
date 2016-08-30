// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Stats.CollectAzureCdnLogs;
using Xunit;
using NuGet.Jobs;
using NuGet.Services.KeyVault;

namespace Tests.Stats.CollectAzureCdnLogs
{
    public class JobTests
    {
        [Fact]
        public async void InitFailsWhenNoArguments()
        {
            var job = new Job();
            var initResult = await job.Init(null);

            Assert.False(initResult);
        }

        [Fact]
        public async void InitFailsWhenEmptyArguments()
        {
            var jobArgsDictionary = CreateEmptyJobArgsDictionary();

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Fact]
        public async void InitSucceedsWhenValidArguments()
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.True(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("http://localhost")]
        [InlineData("ftps://someserver/folder")]
        [InlineData("ftp://")]
        public async void InitFailsForInvalidFtpServerUri(string serverUri)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("FtpSourceUri", serverUri);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async void InitFailsForMissingFtpUsername(string username)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("FtpSourceUsername", username);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async void InitFailsForMissingFtpPassword(string password)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("FtpSourcePassword", password);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("bla")]
        public async void InitFailsForMissingOrInvalidAzureCdnPlatform(string platform)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("AzureCdnPlatform", platform);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async void InitFailsForMissingAzureCdnAccountNumber(string accountNumber)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("AzureCdnAccountNumber", accountNumber);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("bla")]
        public async void InitFailsForMissingOrInvalidAzureCdnCloudStorageAccount(string cloudStorageAccount)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("AzureCdnCloudStorageAccount", cloudStorageAccount);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async void InitFailsForMissingAzureCdnCloudStorageContainerName(string containerName)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Set("AzureCdnCloudStorageContainerName", containerName);

            var job = new Job();
            var initResult = await job.Init(jobArgsDictionary);

            Assert.False(initResult);
        }

        private static IArgumentsDictionary CreateEmptyJobArgsDictionary()
        {
            return CreateArgsDictionaryFromDictionary(new Dictionary<string, string>());
        }

        private static IArgumentsDictionary CreateValidJobArgsDictionary()
        {
            var jobArgsDictionary = new Dictionary<string, string>();
            jobArgsDictionary.Add("FtpSourceUri", "ftp://someserver/logFolder");
            jobArgsDictionary.Add("FtpSourceUsername", @"domain\alias");
            jobArgsDictionary.Add("FtpSourcePassword", "secret");
            jobArgsDictionary.Add("AzureCdnPlatform", "HttpLargeObject");
            jobArgsDictionary.Add("AzureCdnAccountNumber", "AA00");
            jobArgsDictionary.Add("AzureCdnCloudStorageAccount", "UseDevelopmentStorage=true;");
            jobArgsDictionary.Add("AzureCdnCloudStorageContainerName", "cdnLogs");

            return CreateArgsDictionaryFromDictionary(jobArgsDictionary);
        }

        private static IArgumentsDictionary CreateArgsDictionaryFromDictionary(Dictionary<string, string> dictionary)
        {
            return new RefreshingArgumentsDictionary(new SecretReaderFactory().CreateSecretInjector(new EmptySecretReader()), dictionary);
        }
    }
}