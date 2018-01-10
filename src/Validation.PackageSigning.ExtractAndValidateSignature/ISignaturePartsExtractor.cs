﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    /// <summary>
    /// After a package's signature is validated, this interface extracts components of the signature and persists them
    /// to be used by downstream systems.
    /// </summary>
    public interface ISignaturePartsExtractor
    {
        /// <summary>
        /// Extracts and persists artifacts from the provided signed package.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the provided package is not signed.</exception>
        /// <param name="signedPackageReader">The reader of the signed package.</param>
        /// <param name="token">The cancellation token.</param>
        Task ExtractAsync(ISignedPackageReader signedPackageReader, CancellationToken token);
    }
}