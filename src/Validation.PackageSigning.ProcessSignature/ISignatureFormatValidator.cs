// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface ISignatureFormatValidator
    {
        Task<VerifySignaturesResult> ValidateMinimalAsync(
            ISignedPackageReader package,
            CancellationToken token);

        Task<VerifySignaturesResult> ValidateFullAsync(
            ISignedPackageReader package,
            bool hasRepositorySignature,
            CancellationToken token);
    }
}