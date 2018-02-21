// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Validation.PackageCompatibility.Core.Messages
{
    public class PackageCompatibilityValidationMessage
    {
        public PackageCompatibilityValidationMessage(string packageId, string packageVersion, Uri nupkgUri, Guid validationId)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            NupkgUri = nupkgUri;
            ValidationId = validationId;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public Uri NupkgUri { get; }
        public Guid ValidationId { get; }
    }
}
