// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The save operations for symbols need to allow overwrite.
    /// Extend the ValidationFileService and overwrite the Copy methods.
    /// </summary>
    public class ValidationSymbolFileService : ValidationFileService
    {
        private readonly IEntityService<SymbolPackage> _symbolEntityService;

        public ValidationSymbolFileService(
            ICoreFileStorageService fileStorageService,
            IEntityService<SymbolPackage> symbolEntityService,
            IFileDownloader fileDownloader,
            ITelemetryService telemetryService,
            ILogger<ValidationFileService> logger,
            IFileMetadataService fileMetadataService) : base(fileStorageService, fileDownloader, telemetryService, logger, fileMetadataService)
        {
            _symbolEntityService = symbolEntityService ?? throw new ArgumentNullException(nameof(symbolEntityService));
        }

        public override async Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet)
        {
            if(!ShouldCopy(validationSet))
            {
                return;
            }

            await base.CopyValidationPackageToPackageFileAsync(validationSet);
        }

        public override async Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
        {
            if (!ShouldCopy(validationSet))
            {
                return;
            }

            await base.CopyValidationSetPackageToPackageFileAsync(validationSet, destAccessCondition);
        }

        /// <summary>
        /// Copy only if the entity is in a failed state and there is not an existent symbol push already started by the user
        /// or the current validation is in validating state
        /// If the symbols validation would have processors as validators the copy should be done on other states as well.
        /// </summary>
        /// <param name="validationSet">The validation set for the current validation.</param>
        /// <returns>True if the symbolsPackage should be copied to the public container.</returns>
        public bool ShouldCopy(PackageValidationSet validationSet)
        {
            var entityInValidatingState = _symbolEntityService.FindPackageByIdAndVersionStrict(validationSet.PackageId, validationSet.PackageNormalizedVersion);
            var currentEntity = _symbolEntityService.FindPackageByKey(validationSet.PackageKey);

            // If the current entity is in validating mode a new symbolPush is not allowed, so it is safe to copy.
            return currentEntity.Status == PackageStatus.Validating || (entityInValidatingState == null && currentEntity.Status == PackageStatus.FailedValidation);
        }
    }
}