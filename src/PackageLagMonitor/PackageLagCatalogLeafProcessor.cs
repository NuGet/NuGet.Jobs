﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Jobs.Monitoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class PackageLagCatalogLeafProcessor : ICatalogLeafProcessor
    {
        private const string SearchQueryTemplate = "?q=packageid:{0} version:{1}&ignorefilter=true&semverlevel=2.0.0";

        public TimeSpan WaitBetweenPolls = TimeSpan.FromMinutes(2);
        public int RetryLimit = 15;

        private const int FailAfterCommitCount = 10;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;

        private List<Task> _packageProcessTasks;

        private List<Instance> _searchInstances;
        private ISearchServiceClient _searchClient;
        private IPackageLagTelemetryService _telemetryService;

        public PackageLagCatalogLeafProcessor(
            List<Instance> searchInstances,
            ISearchServiceClient searchClient,
            IPackageLagTelemetryService telemetryService,
            ILogger<PackageLagCatalogLeafProcessor> logger)
        {
            _logger = logger;
            _searchInstances = searchInstances;
            _searchClient = searchClient;
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
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, leaf.Published, leaf.Published, expectListed: false, isDelete: true));
            return Task.FromResult(true);
        }

        public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf)
        {
            _packageProcessTasks.Add(ProcessPackageLagDetails(leaf, leaf.Created, leaf.LastEdited, leaf.IsListed(), isDelete: false));
            return Task.FromResult(true);
        }

        public async Task<TimeSpan?> ProcessPackageLagDetails(CatalogLeaf leaf, DateTimeOffset created, DateTimeOffset lastEdited, bool expectListed, bool isDelete)
        {
            var packageId = leaf.PackageId;
            var packageVersion = leaf.PackageVersion;
            TimeSpan? lag;

            _logger.LogInformation("Computing Lag for {PackageId} {PackageVersion}", packageId, packageVersion);
            try
            {
                var cancellationToken = new CancellationToken();
                lag = await GetLagForPackageState(_searchInstances, packageId, packageVersion, expectListed, isDelete, created, lastEdited, cancellationToken);
            }
            catch
            {
                return TimeSpan.FromSeconds(0);
            }

            return lag;

        }

        private async Task<TimeSpan?> GetLagForPackageState(List<Instance> searchInstances, string packageId, string version, bool listed, bool isDelete, DateTimeOffset created, DateTimeOffset lastEdited, CancellationToken token)
        {
            var Tasks = new List<Task<TimeSpan?>>();
            foreach (Instance instance in searchInstances)
            {
                Tasks.Add(ComputeLagForQueries(instance, packageId, version, listed, created, lastEdited, isDelete, token));
            }

            var results = await Task.WhenAll(Tasks);

            try
            {
                var averageTicks = (long)results.Where(r => r.HasValue).Average(t => t.Value.Ticks);

                return new TimeSpan(averageTicks);
            }
            catch(Exception e)
            {
                if (e is InvalidOperationException || e is ArgumentNullException)
                {
                    _logger.LogError("No queries succeeded for {PackageId} {PackageVersion}", packageId, version);
                    return null;
                }
                else
                {
                    throw e;
                }
            }
        }

        private async Task<TimeSpan?> ComputeLagForQueries(
            Instance instance,
            string packageId,
            string packageVersion,
            bool listed,
            DateTimeOffset created,
            DateTimeOffset lastEdited,
            bool deleted,
            CancellationToken token)
        {
            await Task.Yield();

            try
            {
                var resultCount = (long)0;
                var retryCount = (long)0;
                var isListOperation = false;
                var shouldRetry = false;
                TimeSpan createdDelay, v3Delay;
                DateTimeOffset lastReloadTime;

                do
                {
                    var searchResultObject = await _searchClient.GetResultForPackageIdVersion(instance, packageId, packageVersion, token);
                    resultCount = searchResultObject.TotalHits;

                    shouldRetry = false;
                    if (resultCount > 0)
                    {
                        if (deleted)
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
                    else
                    {
                        shouldRetry = !deleted;
                    }
                    if (shouldRetry)
                    {
                        ++retryCount;
                        _logger.LogInformation("Waiting for {RetryTime} seconds before retrying {PackageId} {PackageVersion} against {SearchBaseUrl}", WaitBetweenPolls.TotalSeconds, packageId, packageVersion, instance.BaseQueryUrl);
                        await Task.Delay(WaitBetweenPolls);
                    }
                } while (shouldRetry && retryCount < RetryLimit);


                if (retryCount < RetryLimit)
                {
                    lastReloadTime = await _searchClient.GetIndexLastReloadTimeAsync(instance, token);

                    createdDelay = lastReloadTime - (isListOperation ? lastEdited : created);
                    v3Delay = lastReloadTime - (lastEdited == DateTimeOffset.MinValue ? created : lastEdited);

                    var timeStamp = (isListOperation ? lastEdited : created);

                    // We log both of these values here as they will differ if a package went through validation pipline.
                    _logger.LogInformation("Lag {Timestamp}:{PackageId} {PackageVersion} SearchInstance:{Region}{Instance} Created: {CreatedLag} V3: {V3Lag}", timeStamp, packageId, packageVersion, instance.Region, instance.Index, createdDelay, v3Delay);
                    _logger.LogInformation("LastReload:{LastReloadTimestamp} LastEdited:{LastEditedTimestamp} Created:{CreatedTimestamp} ", lastReloadTime, lastEdited, created);
                    if (!isListOperation)
                    {
                        _telemetryService.TrackPackageCreationLag(timeStamp, instance, packageId, packageVersion, createdDelay);
                    }
                    else
                    {
                        _logger.LogInformation("Skipping log of creation lag for {PackageId} {PackageVersion}. This leaf looks like a list/unlist operation.", packageId, packageVersion);
                    }

                    _telemetryService.TrackV3Lag(timeStamp, instance, packageId, packageVersion, v3Delay);

                    return createdDelay;
                }
                else
                {
                    _logger.LogInformation("Lag check for {PackageId} {PackageVersion} was abandoned. Retry limit reached.", packageId, packageVersion);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to compute lag for {PackageId} {PackageVersion}. {Exception}", packageId, packageVersion, e);
            }

            return null;
        }
    }
}
