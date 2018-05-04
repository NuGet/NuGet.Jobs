// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol.Catalog;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class PackageLagCatalogLeafProcessor : ICatalogLeafProcessor
    {
        private const string SearchQueryTemplate = "?q=packageid:{0} version:{1}&ignorefilter=true&semverlevel=2.0.0";

        private const int WaitInMsBetweenPolls = 120000; // 2 minute

        private const int FailAfterCommitCount = 10;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;

        private List<Task> _packageProcessTasks;

        private int _currentCheckedPackageCount;
        private string _region;
        private string _subscription;
        private List<Instance> _searchInstances;
        private HttpClient _client;
        private IQueueClient _queueClient;

        public PackageLagCatalogLeafProcessor(List<Instance> searchInstances, HttpClient client, IQueueClient queueClient, string region, string subscrption, ILogger<PackageLagCatalogLeafProcessor> logger)
        {
            _logger = logger;
            _searchInstances = searchInstances;
            _client = client;
            _currentCheckedPackageCount = 0;
            _queueClient = queueClient;
            _region = region;
            _subscription = subscrption;
            _packageProcessTasks = new List<Task>();
        }

        public async Task<bool> WaitForProcessing()
        {
            await Task.WhenAll(_packageProcessTasks);
            return true;
        }

        public Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf)
        {
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, /*expectListed*/ false, leaf.Published, DateTimeOffset.MinValue));
            return Task.FromResult(true);
        }

        public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf)
        {
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, leaf.IsListed(), leaf.Created, leaf.LastEdited));
            return Task.FromResult(true);
        }

        private async Task<bool> ProcessPackageLagDetails(CatalogLeaf leaf, bool expectListed, DateTimeOffset created, DateTimeOffset lastEdited)
        {
            var packageId = leaf.PackageId;
            var packageVersion = leaf.PackageVersion;

            _logger.LogInformation("Computing Lag for {0} {1}", packageId, packageVersion);
            try
            {
                var cancellationToken = new CancellationToken();
                var lag = await GetLagForPackageState(_searchInstances, packageId, packageVersion, expectListed, created, lastEdited, cancellationToken);
                ++_currentCheckedPackageCount;

                _logger.LogInformation($"Logging {lag.TotalSeconds} seconds lag for {packageId} {packageVersion}.");
            }
            catch
            {
                return false;
            }

            return true;

        }

        private async Task<TimeSpan> GetLagForPackageState(List<Instance> searchInstances, string packageId, string version, bool listed, DateTimeOffset created, DateTimeOffset lastEdited, CancellationToken token)
        {
            var Tasks = new List<Task<TimeSpan>>();
            foreach (Instance instance in searchInstances)
            {
                var query = instance.BaseQueryUrl + String.Format(SearchQueryTemplate, packageId, version);
                try
                {
                    _logger.LogInformation("Queueing {0}", query);
                    Tasks.Add(ComputeLagForQueries(packageId, version, instance.Index, query, instance.DiagUrl, listed, created, lastEdited, token));
                }
                catch (Exception e)
                {
                    _logger.LogError("An exception was encountered so no HTTP response was returned. {0}", e);
                }
            }

            var results = await Task.WhenAll(Tasks);

            var averageTicks = (long)results.Average<TimeSpan>(t => t.Ticks);

            return new TimeSpan(averageTicks);
        }

        private async Task<TimeSpan> ComputeLagForQueries(
            string packageId,
            string packageVersion,
            int instanceNumber,
            string query,
            string diagUrl,
            bool listed,
            DateTimeOffset created,
            DateTimeOffset lastEdited,
            CancellationToken token)
        {
            await Task.Yield();

            var resultCount = (long)0;
            var retryCount = (long)0;
            var isListOperation = false;
            TimeSpan createdDelay, v3Delay;
            DateTimeOffset lastReloadTime;
            do
            {
                using (var response = await _client.GetAsync(
                    query,
                    HttpCompletionOption.ResponseContentRead,
                    token))
                {
                    var content = response.Content;
                    var searchResultRaw = await content.ReadAsStringAsync();
                    var searchResultObject = JsonConvert.DeserializeObject<SearchResultResponse>(searchResultRaw);

                    resultCount = searchResultObject.TotalHits;

                    if (resultCount > 0)
                    {
                        if (retryCount == 0)
                        {
                            isListOperation = true;
                        }

                        resultCount = searchResultObject.Data[0].Listed == listed ? resultCount : 0;
                    }
                }

                ++retryCount;
                if (resultCount < 1)
                {
                    _logger.LogInformation("Waiting for {0} seconds before retrying", WaitInMsBetweenPolls / 1000);
                    await Task.Delay(WaitInMsBetweenPolls);
                }
            } while (resultCount < 1);


            using (var diagResponse = await _client.GetAsync(
                diagUrl,
                HttpCompletionOption.ResponseContentRead,
                token))
            {
                var diagContent = diagResponse.Content;
                var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                var searchDiagResultObject = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);

                lastReloadTime = searchDiagResultObject.LastIndexReloadTime;
            }

            createdDelay = lastReloadTime - (isListOperation ? lastEdited : created);
            v3Delay = lastReloadTime - (lastEdited == DateTimeOffset.MinValue ? created : lastEdited);

            var timeStamp = (isListOperation ? lastEdited : created);
            var newMessage = new
            {
                PackageId = packageId,
                PackageVersion = packageVersion,
                TimeStamp = timeStamp,
                CreatedDelay = createdDelay,
                V3Delay = v3Delay,
                Region = _region,
                Subscrption = _subscription
            };

            var stringMessage = JsonConvert.SerializeObject(newMessage, Formatting.None);

            _logger.LogInformation("{0}:{1}: Created: {1} V3: {2}", timeStamp, query, createdDelay, v3Delay);
            _logger.LogDebug($"Sending {stringMessage} to queue");
            await _queueClient.SendAsync(new Message(Encoding.UTF8.GetBytes(stringMessage)));

            return createdDelay;
        }
    }
}
