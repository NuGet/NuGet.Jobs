// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Validation;
using Validation.PackageCompatibility.Core.Messages;
using NuGet.Services.Validation.Issues;

namespace Validation.PackageAuthoring.ValidatePackageReferenceCompatibility
{
    public class PackageCompatibilityPackageReferenceValidator : IPackageCompatibilityPackageReferenceValidator
    {
        public async Task<PackageCompatibilityValidatorResult> ValidateAsync(int packageKey, PackageArchiveReader package, PackageCompatibilityValidationMessage message, CancellationToken cancellationToken)
        {
            try
            {
                if (message.PackageId.Equals("newtosoft.json", StringComparison.InvariantCultureIgnoreCase)){
                    var issue = new ClientPackageCompatibilityVerificationIssue("NU1500", "This package is invalid, because we said so.");
                    return AcceptWithIssues(packageKey, message, new IValidationIssue[] { issue });
                }
            }
            catch(Exception e)
            {
                var issue = new ClientPackageCompatibilityVerificationIssue("NU9999", e.Message);
                return AcceptWithIssues(packageKey, message, new IValidationIssue[] { issue });
            }

            return AcceptWithoutErrorsAsync(packageKey, message);
        }

        private PackageCompatibilityValidatorResult AcceptWithIssues(int packageKey,
            PackageCompatibilityValidationMessage message, IReadOnlyList<IValidationIssue> issues)
        {
            return new PackageCompatibilityValidatorResult(ValidationStatus.Succeeded, issues);
        }

        private PackageCompatibilityValidatorResult AcceptWithoutErrorsAsync(
            int packageKey,
            PackageCompatibilityValidationMessage message)
        {
            return new PackageCompatibilityValidatorResult(ValidationStatus.Succeeded);
        }
    }
}
