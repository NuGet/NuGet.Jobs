// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// A service used to persist a <see cref="IValidator"/>'s validation statuses.
    /// </summary>
    public interface IValidatorStateService
    {
        /// <summary>
        /// Get the persisted <see cref="ValidatorStatus"/> for the given <see cref="IValidationRequest"/>.
        /// </summary>
        /// <param name="validatorName">The unique name for the validator performing the validation.</param>
        /// <param name="request">The request whose status should be fetched.</param>
        /// <returns>The persisted status of the validation request, or, a new ValidatorStatus if no status has been persisted.</returns>
        Task<ValidatorStatus> GetStatusAsync(string validatorName, IValidationRequest request);

        /// <summary>
        /// Check if the request intends to revalidate a package that has already been validated by <see cref="TValidator"/> by
        /// a different validation request.
        /// </summary>
        /// <param name="validatorName">The unique name for the validator performing the validation.</param>
        /// <param name="request">The package validation request.</param>
        /// <returns>Whether the <see cref="TValidator"/> has already validated this request's package in a different validation request.</returns>
        Task<bool> IsRevalidationRequestAsync(string validatorName, IValidationRequest request);

        /// <summary>
        /// Persist the status of a new validation request.
        /// </summary>
        /// <param name="validatorName">The unique name for the validator performing the validation.</param>
        /// <param name="status">The status of the given validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        Task AddStatusAsync(string validatorName, ValidatorStatus status);

        /// <summary>
        /// Persist the status of an already existing validation request.
        /// </summary>
        /// <param name="validatorName">The unique name for the validator performing the validation.</param>
        /// <param name="status">The updated status for the validation request.</param>
        /// <returns>A task that completes when the status has been persisted.</returns>
        /// <exception cref="System.Data.Entity.Infrastructure.DbUpdateConcurrencyException">Thrown when the status had already been updated.</exception>
        Task SaveStatusAsync(string validatorName, ValidatorStatus status);
    }
}
