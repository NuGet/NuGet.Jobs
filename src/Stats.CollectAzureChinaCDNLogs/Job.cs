// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CollectAzureChinaCDNLogs
{
    public class Job : JobBase
    {
        private const int DefaultExecutionTimeoutInSeconds = 14400; // 4 hours
        private const int MaxFilesToProcess = 4;

        private CloudStorageAccount _cloudStorageAccountSource;
        private CloudStorageAccount _cloudStorageAccountDestination;
        private string _cloudStorageContainerNameDestination;
        private string _cloudStorageContainerNameSource;
        private Collector _chinaCollector;
        private int _executionTimeoutInSeconds;
        private string _destinationFilePrefix;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            //var collector = new ChinaStatsCollector();
            //string input = "40.125.202.231,7/27/2017 4:50:09 PM +00:00,GET, \"/v3-flatcontainer/system.net.primitives/index.json\",HTTP/1.1,200,1196,\"-\",\"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\",133,TCP_MISS,118.180.6.168";
            //string output = collector.TransformRawLogLine(input).ToString();


            var cloudStorageAccountConnStringSource = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureStorageAccountConnectionStringSource);
            var cloudStorageAccountConnStringDest = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureStorageAccountConnectionStringDestination);
            _cloudStorageAccountSource = ValidateAzureCloudStorageAccount(cloudStorageAccountConnStringSource);
            _cloudStorageAccountDestination = ValidateAzureCloudStorageAccount(cloudStorageAccountConnStringDest);
            _cloudStorageContainerNameDestination = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureContainerNameDestination);
            _cloudStorageContainerNameSource = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureContainerNameSource);
            _destinationFilePrefix = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.DestinationFilePrefix);
            _executionTimeoutInSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, ArgumentNames.AzureContainerNameSource) ?? DefaultExecutionTimeoutInSeconds;

            var source = new AzureStatsLogSource(cloudStorageAccountConnStringSource, _cloudStorageContainerNameSource, _executionTimeoutInSeconds/MaxFilesToProcess);
            var dest = new AzureStatsLogDestination(cloudStorageAccountConnStringDest,_cloudStorageContainerNameDestination);
            _chinaCollector = new ChinaStatsCollector(source, dest);
        }

        public override async Task Run()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(_executionTimeoutInSeconds*1000);
            var aggregateException = await _chinaCollector.TryProcessAsync(MaxFilesToProcess, s => $"{_destinationFilePrefix}_{s}", ContentType.GZip, cts.Token);

            if (aggregateException != null)
            {
                foreach(var ex in aggregateException.InnerExceptions)
                {
                    Logger.LogError(ex, ex.Message);
                }
            }
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }
    }
}
