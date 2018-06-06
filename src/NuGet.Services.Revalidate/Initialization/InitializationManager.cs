// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using IGalleryContext = IEntitiesContext;

    public class InitializationManager
    {
        private static int BatchSize = 1000;

        private readonly IRevalidationStateService _revalidationState;
        private readonly IPackageFinder _packageFinder;
        private readonly InitializationConfiguration _config;
        private readonly ILogger<PackageFinder> _logger;

        public InitializationManager(
            IRevalidationStateService revalidationState,
            IPackageFinder packageFinder,
            InitializationConfiguration config,
            ILogger<PackageFinder> logger)
        {
            // TODO: Accept service for settings (IsInitialized, etc...)
            // See: https://github.com/NuGet/Engineering/issues/1440
            _revalidationState = revalidationState ?? throw new ArgumentNullException(nameof(revalidationState));
            _packageFinder = packageFinder ?? throw new ArgumentNullException(nameof(packageFinder));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            // TODO: Check "IsInitialized" setting. If true, error!
            await ClearPackageRevalidationStateAsync();

            // Find packages owned by Microsoft or preinstalled by Visual Studio.
            var microsoftPackages = _packageFinder.FindMicrosoftPackages();
            var preinstalledPackages = _packageFinder.FindPreinstalledPackages(except: microsoftPackages);

            // Find the packages depended by both Microsoft and preinstalled packages.
            var microsoftOrPreinstalledPackages = new CaseInsensitiveSet();

            microsoftOrPreinstalledPackages.UnionWith(microsoftPackages);
            microsoftOrPreinstalledPackages.UnionWith(preinstalledPackages);

            var dependencyPackages = _packageFinder.FindDependencyPackages(microsoftOrPreinstalledPackages);

            // Find all other package ids.
            var knownPackages = new CaseInsensitiveSet();

            knownPackages.UnionWith(microsoftOrPreinstalledPackages);
            knownPackages.UnionWith(dependencyPackages);

            var remainingPackages = _packageFinder.FindAllPackages(except: knownPackages);

            // Save the packages that were found, by order of priority.
            await InitializePackageSetAsync("Microsoft", microsoftPackages);
            await InitializePackageSetAsync("Preinstalled", preinstalledPackages);
            await InitializePackageSetAsync("Dependency", dependencyPackages);
            await InitializePackageSetAsync("Remaining", remainingPackages);

            // TODO: Set "IsInitialized" setting to true
        }

        private async Task ClearPackageRevalidationStateAsync()
        {
            _logger.LogInformation("Clearing package revalidation state, if it exists...");

            int removedRevalidations;

            do
            {
                removedRevalidations = await _revalidationState.RemoveRevalidationsAsync(BatchSize);

                if (removedRevalidations > 0)
                {
                    _logger.LogInformation(
                        "Deleted {RemovedRevalidations} revalidations. Sleeping for {SleepDuration}...",
                        removedRevalidations,
                        _config.SleepDurationBetweenBatches);

                    await Task.Delay(_config.SleepDurationBetweenBatches);
                }
            }
            while (removedRevalidations == BatchSize);

            _logger.LogInformation("Cleared package revalidation state");
        }

        private async Task InitializePackageSetAsync(string setName, CaseInsensitiveSet packageIds)
        {
            // Order the set of package ids by downloads, and then batch them into manageable chunks.
            _logger.LogInformation(
                "Partitioning package set {SetName} into chunks...",
                setName);

            var chunks = _packageFinder.FindPackageInformation(setName, packageIds)
                .OrderByDescending(p => p.Downloads)
                .WeightedBatch(BatchSize, p => p.Versions)
                .Select(b => b.Select(p => p.Id).ToList())
                .ToList();

            _logger.LogInformation(
                "Partitioned package set {SetName} into {Chunks} chunks",
                setName,
                chunks.Count);

            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                // TODO: Check the kill switch
                _logger.LogInformation("Initializing chunk {Chunk} of {Chunks} for package set {SetName}...",
                    chunkIndex + 1,
                    chunks.Count,
                    setName);

                var chunk = chunks[chunkIndex];
                var versions = _packageFinder.FindAppropriateVersions(chunk);

                await InitializeRevalidationsAsync(chunk, versions);

                _logger.LogInformation("Initialized chunk {Chunk} of {Chunks} for package set {SetName}",
                    chunkIndex + 1,
                    chunks.Count,
                    setName);

                // Sleep if this is not the last chunk to prevent overloading the database.
                if (chunkIndex < chunks.Count - 1)
                {
                    _logger.LogInformation(
                        "Sleeping for {SleepDuration} before initializing the next chunk...",
                        _config.SleepDurationBetweenBatches);

                    await Task.Delay(_config.SleepDurationBetweenBatches);
                }
            }

            _logger.LogInformation("Finished initializing package set {SetName}", setName);
        }

        private async Task InitializeRevalidationsAsync(List<string> packageIds, Dictionary<string, List<NuGetVersion>> versions)
        {
            var revalidations = new List<PackageRevalidation>();

            foreach (var packageId in packageIds)
            {
                if (!versions.ContainsKey(packageId) || versions[packageId].Count == 0)
                {
                    _logger.LogError("Could not find any versions of package {PackageId} to revalidate", packageId);

                    throw new InvalidOperationException($"Could not find any versions of package {packageId} to revalidate");
                }

                foreach (var version in versions[packageId])
                {
                    revalidations.Add(new PackageRevalidation
                    {
                        PackageId = packageId,
                        PackageNormalizedVersion = version.ToNormalizedString(),
                        ValidationTrackingId = Guid.NewGuid(),
                        Completed = false,
                        Enqueued = null,
                    });
                }
            }

            await _revalidationState.AddPackageRevalidationsAsync(revalidations);
        }
    }
}
