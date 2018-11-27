using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : EntityStatusProcessor<Package>
    {
        public PackageStatusProcessor(
            IEntityService<Package> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<EntityStatusProcessor<Package>> logger) 
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
        }

        protected override async Task OnBeforeUpdateDatabaseToMakePackageAvailable(
            IValidatingEntity<Package> validatingEntity,
            PackageValidationSet validationSet)
        {
            // TODO: extract license file
        }
    }
}
