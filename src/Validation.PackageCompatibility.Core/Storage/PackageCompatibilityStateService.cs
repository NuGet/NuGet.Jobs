using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Services.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validation.PackageCompatibility.Core.Storage
{
    public class PackageCompatibilityStateService : IPackageCompatibilityService
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ILogger<PackageCompatibilityStateService> _logger;

        public PackageCompatibilityStateService(
            IValidationEntitiesContext validationContext,
            ILogger<PackageCompatibilityStateService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetPackageCompatibilityState(
           Guid validationId,
           IReadOnlyList<PackLogMessage> messages)
        {

            foreach (var log in messages)
            {
                warnings.Add(
                    new PackageCompatibilityIssue()
                    {
                                // Key - will the DB apply is
                                ClientIssueCode = log.Code.ToString(),
                        Data = log.Message,
                        PackageValidationKey = message.ValidationId
                    }
                    );

                if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(nameof(packageId));
            }
            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(nameof(packageVersion));
            }

            // It is possible this package has already been validated. If so, the package's state will already exist
            // in the database. Updates to this state should only be requested on explicit revalidation gestures. However,
            // this invariant may be broken due to message duplication.
            await _validationContext.PackageCom.FirstOrDefaultAsync(s => s.PackageKey == packageKey);
{            }
        }
    }
}
