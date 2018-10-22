// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class SymbolsStatusProcessor : EntityStatusProcessor<SymbolPackage>
    {
        public SymbolsStatusProcessor(
            IEntityService<SymbolPackage> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<EntityStatusProcessor<SymbolPackage>> logger) 
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
        }

        protected override async Task MakePackageAvailableAsync(IValidatingEntity<SymbolPackage> validatingEntity, PackageValidationSet validationSet)
        {
            if(!ProceedToMakePackageAvailable(validatingEntity, validationSet))
            {
                _logger.LogInformation("SymbolsPackage PackageId { PackageId} PackageVersion {PackageVersion} Status {Status} was not made available again.",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validatingEntity.Status);
                return;
            }
            await base.MakePackageAvailableAsync(validatingEntity, validationSet);
        }

        /// <summary>
        /// Proceed to change the state only if:
        /// 1.the entity is in a failed state and there is not an existent symbol push already started by the user. This state can happen on revalidation only.
        /// or 
        /// 2. the current validation is in validating state
        /// If the symbols validation would have processors as validators the copy should be done on other states as well.
        /// </summary>
        /// <param name="validationSet">The validation set for the current validation.</param>
        /// <returns>True if the package should be made available (copied to the public container, db updated etc.)</returns>
        public bool ProceedToMakePackageAvailable(IValidatingEntity<SymbolPackage> validatingEntity, PackageValidationSet validationSet)
        {
            var entityInValidatingState = _galleryPackageService.FindPackageByIdAndVersionStrict(validationSet.PackageId, validationSet.PackageNormalizedVersion);
            var currentEntity = validatingEntity;

            // If the current entity is in validating mode a new symbolPush is not allowed, so it is safe to copy.
            var aNewEntityInValidatingStateExists = entityInValidatingState != null;

            var proceed = currentEntity.Status == PackageStatus.Validating || (!aNewEntityInValidatingStateExists && currentEntity.Status == PackageStatus.FailedValidation);
            _logger.LogInformation("Proceed to make symbols available check: "
                + "PackageId {PackageId} "
                + "PackageVersion {PackageVersion} "
                + "ValidationTrackingId {ValidationTrackingId} "
                + "CurrentValidating entity status {CurrentEntityStatus}"
                + "ANewEntityInValidatingStateExists {ANewEntityInValidatingStateExists}"
                + "Proceed {Proceed}",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId,
                currentEntity.Status,
                aNewEntityInValidatingStateExists,
                proceed
                );
            return proceed;
        }
    }
}
