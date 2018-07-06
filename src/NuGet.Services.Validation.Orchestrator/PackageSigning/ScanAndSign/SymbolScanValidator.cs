// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;


namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    [ValidatorName(ValidatorName.SymbolScan)]
    public class SymbolScanValidator : ScanAndSignProcessor<SymbolPackage>
    {
        public SymbolScanValidator(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            IEntityService<SymbolPackage> galleryService,
            ICriteriaEvaluator<SymbolPackage> criteriaEvaluator,
            IScanAndSignEnqueuer scanAndSignEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            IOptionsSnapshot<ScanAndSignConfiguration> configurationAccessor,
            ILogger<ScanAndSignProcessor<SymbolPackage>> logger) : base (validationContext,
                validatorStateService,
                galleryService,
                criteriaEvaluator,
                scanAndSignEnqueuer,
                blobProvider,
                configurationAccessor,
                logger)
        {
        }
    }
}
