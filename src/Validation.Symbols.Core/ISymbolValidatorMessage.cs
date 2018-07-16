// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    interface ISymbolValidatorMessage
    {
        /// <summary>
        /// The Validation id 
        /// Validation Id will be unique per validator validation.
        /// It should be a 1:1 matching between ValidationId and PackageValidation.Key 
        /// </summary>
        Guid ValidationId { get; }

        /// <summary>
        /// The id of the symbol package.
        /// </summary>
        int SymbolPackageKey { get; }

        /// <summary>
        /// The package Id.
        /// </summary>
        string PackageId { get; }

        /// <summary>
        /// The package normalized version.
        /// </summary>
        string PackageNormalizedVersion { get; }

        /// <summary>
        /// The Url of the snupkg. 
        /// </summary>
        string SNupkgUrl { get; }
    }
}
