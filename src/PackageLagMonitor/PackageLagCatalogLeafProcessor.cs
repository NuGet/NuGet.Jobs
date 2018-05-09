// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Jobs.Montoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public class PackageLagCatalogLeafProcessor : ICatalogLeafProcessor
    {
        private const string SearchQueryTemplate = "?q=packageid:{0} version:{1}&ignorefilter=true&semverlevel=2.0.0";

        private const int WaitInMsBetweenPolls = 120000; // 2 minute
        private const int MAX_RETRY_COUNT = 15;

        private const int FailAfterCommitCount = 10;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;

        private List<Task> _packageProcessTasks;

        private List<Instance> _searchInstances;
        private HttpClient _client;
        private IPackageLagTelemetryService _telemetryService;

        public PackageLagCatalogLeafProcessor(
            List<Instance> searchInstances,
            HttpClient client,
            IPackageLagTelemetryService telemetryService,
            ILogger<PackageLagCatalogLeafProcessor> logger)
        {
            _logger = logger;
            _searchInstances = searchInstances;
            _client = client;
            _telemetryService = telemetryService;
            _packageProcessTasks = new List<Task>();
        }

        public async Task<bool> WaitForProcessing()
        {
            await Task.WhenAll(_packageProcessTasks);
            return true;
        }

        public Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf)
        {
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, leaf.Published, DateTimeOffset.MinValue, expectListed: false));
            return Task.FromResult(true);
        }

        public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf)
        {
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, leaf.Created, leaf.LastEdited, leaf.IsListed()));
            return Task.FromResult(true);
        }

        private async Task<bool> ProcessPackageLagDetails(CatalogLeaf leaf, DateTimeOffset created, DateTimeOffset lastEdited, bool expectListed)
        {
            var packageId = leaf.PackageId;
            var packageVersion = leaf.PackageVersion;

            _logger.LogInformation("Computing Lag for {PackageId} {PackageVersion}", packageId, packageVersion);
            try
            {
                var cancellationToken = new CancellationToken();
                var lag = await GetLagForPackageState(_searchInstances, packageId, packageVersion, expectListed, created, lastEdited, cancellationToken);

                if (lag.Ticks > 0)
                {
                    _logger.LogInformation("Logging {LagInSeconds} seconds lag for {PackageId} {PackageVersion}.", lag.TotalSeconds, packageId, packageVersion);
                }
                else
                {
                    _logger.LogInformation("Stopped checking lag for {PackageId} {PackageVersion} after {RetryCount} retries.", packageId, packageVersion, MAX_RETRY_COUNT);
                }
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
                Tasks.Add(ComputeLagForQueries(instance, packageId, version, listed, created, lastEdited, token));
            }

            var results = await Task.WhenAll(Tasks);

            var averageTicks = (long)results.Average<TimeSpan>(t => t.Ticks);

            return new TimeSpan(averageTicks);
        }

        private async Task<TimeSpan> ComputeLagForQueries(
            Instance instance,
            string packageId,
            string packageVersion,
            bool listed,
            DateTimeOffset created,
            DateTimeOffset lastEdited,
            CancellationToken token)
        {
            await Task.Yield();
            
            try
            {
                var query = instance.BaseQueryUrl + String.Format(SearchQueryTemplate, packageId, packageVersion);
                var resultCount = (long)0;
                var retryCount = (long)0;
                var isListOperation = false;
                var shouldRetry = false;
                TimeSpan createdDelay, v3Delay;
                DateTimeOffset lastReloadTime;


                _logger.LogInformation("Queueing {Query}", query);
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

                        shouldRetry = false;
                        if (resultCount > 0)
                        {
                            if (lastEdited == DateTime.MinValue)
                            {
                                shouldRetry = true;
                            }
                            else
                            {
                                if (retryCount == 0)
                                {
                                    isListOperation = true;
                                }

                                shouldRetry = searchResultObject.Data[0].LastEdited < lastEdited;
                            }
                        }
                    }

                    ++retryCount;
                    if (shouldRetry)
                    {
                        _logger.LogInformation("Waiting for {RetryTime} seconds before retrying {Query}", WaitInMsBetweenPolls / 1000, query);
                        await Task.Delay(WaitInMsBetweenPolls);
                    }
                } while (shouldRetry && retryCount < MAX_RETRY_COUNT);


                if (retryCount < MAX_RETRY_COUNT)
                {
                    using (var diagResponse = await _client.GetAsync(
                        instance.DiagUrl,
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


                    // We log both of these values here as they will differ if a package went through validation pipline.
                    _logger.LogInformation("{Timestamp}:{Query}: Created: {CreatedLag} V3: {V3Lag}", timeStamp, query, createdDelay, v3Delay);
                    _telemetryService.TrackPackageCreationLag(timeStamp, instance, packageId, packageVersion, createdDelay);
                    _telemetryService.TrackV3Lag(timeStamp, instance, packageId, packageVersion, v3Delay);

                    return createdDelay;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("An exception was encountered so no HTTP response was returned. {Exception}", e);
            }

            return new TimeSpan(0);
        }
    }
}
