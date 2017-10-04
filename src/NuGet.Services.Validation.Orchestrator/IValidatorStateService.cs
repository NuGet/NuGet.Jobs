// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// A service used to persist a <see cref="IValidator"/>'s validation statuses.
    /// </summary>
    /// <typeparam name="TValidator">The validator whose statuses this service persists.</typeparam>
    public interface IValidatorStateService<TValidator>
        where TValidator : IValidator
    {
        /// <summary>
        /// Get the persisted <see cref="ValidationStatus"/> for the given <see cref="IValidationRequest"/>.
        /// </summary>
        /// <param name="request">The request whose status should be fetched.</param>
        /// <returns>The persisted status of the validation request.</returns>
        ValidationStatus GetStatus(IValidationRequest request);

        /// <summary>
        /// Check if the request intends to revalidate a package that has already been validated by <see cref="TValidator"/>.
        /// </summary>
        /// <param name="request">The package validation request.</param>
        /// <returns>Whether the <see cref="TValidator"/> has already validated this request's package.</returns>
        bool IsRevalidationRequest(IValidationRequest request);

        /// <summary>
        /// Persist the status of a new validation request.
        /// </summary>
        /// <param name="request">The validation request whose state has never been persisted before.</param>
        /// <param name="status">The status of the given validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task AddStatusAsync(IValidationRequest request, ValidationStatus status);

        /// <summary>
        /// Persist the status of an already existing validation request.
        /// </summary>
        /// <param name="request">The validation request whose status will be updated.</param>
        /// <param name="status">The updated status for the validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task SaveStatusAsync(IValidationRequest request, ValidationStatus status);
    }
}
