// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class PackageSigningStateService
        : IPackageSigningStateService
    {
        private const int UniqueConstraintViolationErrorCode = 2627;
        private readonly IValidationEntitiesContext _validationContext;

        public PackageSigningStateService(IValidationEntitiesContext validationContext)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
        }

        public async Task<SavePackageSigningStateResult> TrySetPackageSigningState(
            int packageKey, 
            SignatureValidationMessage message, 
            bool isRevalidationRequest, 
            PackageSigningStatus status)
        {
            // Check for revalidation
            if (isRevalidationRequest)
            {
                // Update existing record
                var currentState = _validationContext.PackageSigningStates.FirstOrDefault(s => s.PackageKey == packageKey);
                currentState.SigningStatus = status;
            }
            else
            {
                // Insert new record
                var currentState = new PackageSigningState
                {
                    PackageId = message.PackageId,
                    PackageKey = packageKey,
                    PackageNormalizedVersion = NormalizePackageVersion(message.PackageVersion),
                    SigningStatus = status
                };

                _validationContext.PackageSigningStates.Add(currentState);
            }

            try
            {
                await _validationContext.SaveChangesAsync();

                return SavePackageSigningStateResult.Success;
            }
            catch (DbUpdateException e) when (IsUniqueConstraintViolationException(e))
            {
                return SavePackageSigningStateResult.StatusAlreadyExists;
            }
        }

        private static string NormalizePackageVersion(string packageVersion)
        {
            return NuGetVersion
                .Parse(packageVersion)
                .ToNormalizedString()
                .ToLowerInvariant();
        }

        private static bool IsUniqueConstraintViolationException(DbUpdateException e)
        {
            if (e.GetBaseException() is SqlException sqlException)
            {
                return sqlException.Errors.Cast<SqlError>().Any(error => error.Number == UniqueConstraintViolationErrorCode);
            }

            return false;
        }
    }
}
