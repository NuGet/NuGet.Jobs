// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Symbols
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

        public override async Task<bool> ShouldRepositorySignAsync(IValidationRequest request, List<string> owners)
        {
            return await Task.FromResult(false);
        }
    }
}
