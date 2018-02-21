// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;
using System.Threading;
using System.Threading.Tasks;
using Validation.PackageCompatibility.Core.Messages;

namespace Validation.PackageAuthoring.ValidatePackageReferenceCompatibility
{
    interface IPackageCompatibilityPackageReferenceValidator
    {
        Task<PackageCompatibilityValidatorResult> ValidateAsync(
            int packageKey,
            PackageArchiveReader package,
            PackageCompatibilityValidationMessage message,
            CancellationToken cancellationToken);
    }
}
