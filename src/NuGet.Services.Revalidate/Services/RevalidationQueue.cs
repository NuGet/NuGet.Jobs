// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using IGalleryContext = IEntitiesContext;

    public class RevalidationQueue : IRevalidationQueue
    {
        private readonly IGalleryContext _galleryContext;
        private readonly IValidationEntitiesContext _validationContext;
        private readonly RevalidationQueueConfiguration _config;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<RevalidationQueue> _logger;

        public RevalidationQueue(
            IGalleryContext galleryContext,
            IValidationEntitiesContext validationContext,
            RevalidationQueueConfiguration config,
            ITelemetryService telemetry,
            ILogger<RevalidationQueue> logger)
        {
            _galleryContext = galleryContext ?? throw new ArgumentNullException(nameof(galleryContext));
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<PackageRevalidation>> NextAsync()
        {
            // Find the next package to revalidate. We will skip packages if:
            //   1. The package has more than "MaximumPackageVersions" versions
            //   2. The package has already been enqueued for revalidation
            //   3. The package's revalidation was completed by an external factory (like manual admin revalidation)
            IQueryable<PackageRevalidation> query = _validationContext.PackageRevalidations;

            if (_config.MaximumPackageVersions.HasValue)
            {
                query = query.Where(
                    r =>
                    !_validationContext.PackageRevalidations.GroupBy(r2 => r2.PackageId)
                    .Where(g => g.Count() > _config.MaximumPackageVersions)
                    .Any(g => g.Key == r.PackageId));
            }

            var next = await query
                .Where(r => r.Enqueued == null)
                .Where(r => r.Completed == false)
                .OrderBy(r => r.Key)
                .Take(_config.MaxBatchSize)
                .ToListAsync();

            if (!next.Any())
            {
                _logger.LogWarning("Could not find any incomplete revalidations");
                return next;
            }

            // Return all the revalidations that aren't already completed.
            return await FilterCompletedRevalidationsAsync(next);
        }

        private async Task<List<PackageRevalidation>> FilterCompletedRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            // Split the list of revalidations by which ones have been completed.
            var completed = new List<PackageRevalidation>();
            var uncompleted = revalidations.ToDictionary(
                r => Tuple.Create(r.PackageId, r.PackageNormalizedVersion),
                r => r);

            // Seperate out packages that already have a repository signature.
            var hasRepositorySignatures = await _validationContext.PackageSigningStates
                .Where(s => revalidations.Any(r => r.PackageId == s.PackageId && r.PackageNormalizedVersion == s.PackageNormalizedVersion))
                .Where(s => s.PackageSignatures.Any(sig => sig.Type == PackageSignatureType.Repository))
                .Select(s => new { s.PackageId, s.PackageNormalizedVersion })
                .ToListAsync();

            foreach (var package in hasRepositorySignatures)
            {
                var key = Tuple.Create(package.PackageId, package.PackageNormalizedVersion);

                completed.Add(uncompleted[key]);
                uncompleted.Remove(key);
            }

            // Separate out packages that are no longer available. We consider that a revalidation
            // is "completed" if a package no longer exists.
            var packageStatuses = await _galleryContext.Set<Package>()
                .Where(p => uncompleted.Any(r => r.Key.Item1 == p.PackageRegistration.Id && r.Key.Item2 == p.NormalizedVersion))
                .ToDictionaryAsync(
                    p => Tuple.Create(p.PackageRegistration.Id, p.NormalizedVersion),
                    p => p.PackageStatusKey);

            foreach (var key in uncompleted.Keys.ToList())
            {
                // Packages that are hard deleted won't have a status.
                if (!packageStatuses.TryGetValue(key, out var status) || status == PackageStatus.Deleted)
                {
                    completed.Add(uncompleted[key]);
                    uncompleted.Remove(key);
                    continue;
                }
            }

            // Update revalidations that were determined to be completed and return the remaining revalidations.
            await MarkRevalidationsAsCompletedAsync(completed);
            return uncompleted.Values.ToList();
        }

        private async Task MarkRevalidationsAsCompletedAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            try
            {
                foreach (var revalidation in revalidations)
                {
                    _logger.LogInformation(
                        "Marking package revalidation as completed as it has a repository signature or is deleted for {PackageId} {PackageNormalizedVersion}...",
                        revalidation.PackageId,
                        revalidation.PackageNormalizedVersion);

                    revalidation.Completed = true;
                }

                await _validationContext.SaveChangesAsync();

                foreach (var revalidation in revalidations)
                {
                    _logger.LogInformation(
                        "Marked package revalidation as completed as it has a repository signature or is deleted for {PackageId} {PackageNormalizedVersion}",
                        revalidation.PackageId,
                        revalidation.PackageNormalizedVersion);

                    _telemetry.TrackPackageRevalidationMarkedAsCompleted(revalidation.PackageId, revalidation.PackageNormalizedVersion);
                }
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogError(
                    0,
                    e,
                    "Failed to mark package revalidations as completed. " +
                    $"These revalidations will be marked as completed on the next iteration of {nameof(NextAsync)}...");
            }
        }
    }
}
