// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Validation.Symbols;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;

using NuGet.Services.Validation.Vcs;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolScanValidatorFacts
    {
        public abstract class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<ICoreSymbolPackageService> _galleryService;
            protected readonly Mock<ICriteriaEvaluator<Package>> _criteriaEvaluator;
            protected readonly Mock<IScanAndSignEnqueuer> _scanAndSignEnqueuer;
            protected readonly Mock<IOptionsSnapshot<ScanAndSignConfiguration>> _configurationAccessor;
            protected readonly Mock<IValidatorStateService> _validatorStateService;
            protected readonly ILogger<ScanAndSignProcessor>_logger;
            protected readonly SymbolScanValidator _target;

            public FactsBase(ITestOutputHelper output)
            {
                _validationContext = new Mock<IValidationEntitiesContext>();
                _galleryService = new Mock<ICoreSymbolPackageService>();
                _criteriaEvaluator = new Mock<ICriteriaEvaluator<Package>>();
                _scanAndSignEnqueuer = new Mock<IScanAndSignEnqueuer>();
                _configurationAccessor = new Mock<IOptionsSnapshot<ScanAndSignConfiguration>>();
                _validatorStateService = new Mock<IValidatorStateService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<ScanAndSignProcessor>();

                _configurationAccessor.Setup(c => c.Value).Returns(new ScanAndSignConfiguration());

                _target = new SymbolScanValidator(
                    _validationContext.Object,
                    _validatorStateService.Object,
                    _galleryService.Object,
                    _criteriaEvaluator.Object,
                    _scanAndSignEnqueuer.Object,
                    _configurationAccessor.Object,
                    _logger);
            }
        }
    }
}
