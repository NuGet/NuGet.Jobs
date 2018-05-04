// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// The processor that strips unacceptable repository signatures and then validates signed packages.
    /// </summary>
    [ValidatorName(ValidatorName.PackageSigningProcessor)]
    public class PackageSigningProcessor : BaseProcessSignature, IProcessor
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IProcessSignatureEnqueuer _signatureVerificationEnqueuer;
        private readonly ISimpleCloudBlobProvider _blobProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageSigningProcessor> _logger;

        public PackageSigningProcessor(
            IValidatorStateService validatorStateService,
            IProcessSignatureEnqueuer signatureVerificationEnqueuer,
            ISimpleCloudBlobProvider blobProvider,
            ITelemetryService telemetryService,
            ILogger<PackageSigningProcessor> logger)
          : base(validatorStateService, signatureVerificationEnqueuer, blobProvider, telemetryService, logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureVerificationEnqueuer = signatureVerificationEnqueuer ?? throw new ArgumentNullException(nameof(signatureVerificationEnqueuer));
            _blobProvider = blobProvider ?? throw new ArgumentNullException(nameof(blobProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override bool RequiresRepositorySignature => false;
    }
}
