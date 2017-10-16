// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public interface IPackageSigningStateService
    {
        Task<SavePackageSigningStateResult> TrySetPackageSigningState(
            int packageKey, 
            SignatureValidationMessage message, 
            bool isRevalidationRequest, 
            PackageSigningStatus status);
    }
}
