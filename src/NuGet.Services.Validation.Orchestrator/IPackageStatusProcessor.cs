// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// This interface manages the state of gallery artifacts: gallery DB and packages container.
    /// </summary>
    public interface IStatusProcessor<T> where T : class, IEntity
    {
        Task<SetStatusResult> SetStatusAsync(
            IValidatingEntity<T> validatingEntity,
            PackageValidationSet validationSet,
            PackageStatus status);
    }

    /// <summary>
    /// The result of <see cref="IStatusProcessor{T}.SetStatusAsync(IValidatingEntity{T}, PackageValidationSet, PackageStatus)"/>.
    /// </summary>
    public enum SetStatusResult
    {
        // The set status operation was completed successfully.
        Completed,

        // The set status operation was cancelled gacefully. The validation set should
        // no longer be processed.
        Cancelled,
    }
}