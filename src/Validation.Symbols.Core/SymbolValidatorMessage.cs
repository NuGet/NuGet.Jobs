// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolValidatorMessage : ISymbolValidatorMessage
    {
        public SymbolValidatorMessage(Guid validationId, 
            int symbolPackageKey,
            string packageId,
            string packageNormalizedVersion,
            string snupkgUrl)
        {
            ValidationId = validationId;
            SymbolPackageKey = symbolPackageKey;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            SNupkgUrl = snupkgUrl;
        }

        public Guid ValidationId { get; }

        public int SymbolPackageKey { get; }

        public string PackageId { get; }

        public string PackageNormalizedVersion { get; }

        public string SNupkgUrl { get; }
    }
}
