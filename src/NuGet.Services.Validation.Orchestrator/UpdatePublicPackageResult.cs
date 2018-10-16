// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The result of calling <see cref="EntityStatusProcessor{T}.MakePackageAvailableAsync(IValidatingEntity{T}, PackageValidationSet)"/>.
    /// </summary>
    internal enum UpdatePublicPackageResult
    {
        /// <summary>
        /// The update was successful and the validation set's package was copied to the public destination.
        /// </summary>
        Copied = 0,

        /// <summary>
        /// The update was successful without copying.
        /// </summary>
        Skipped = 1,

        /// <summary>
        /// The update failed. The copy operation's ETag did not match public destination's ETag.
        /// </summary>
        AccessConditionFailed = 2,
    }
}