// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Validation.PackageCompatibility.Core.Messages
{
    public class PackageCompatibilityValidationMessage
    {
        public PackageCompatibilityValidationMessage(string packageId, string packageVersion, Uri nupkgUri, Guid validationId)
        {
            if (validationId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationId));
            }
            ValidationId = validationId;
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            PackageVersion = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            NupkgUri = nupkgUri ?? throw new ArgumentNullException(nameof(nupkgUri));
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public Uri NupkgUri { get; }
        public Guid ValidationId { get; }
    }
}
