// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;
using Validation.PackageCompatibility.Core.Messages;

namespace NuGet.Services.Validation.PackageCompatibility
{
    /// <summary>
    /// Kicks off package compatibility verifications.
    /// </summary>
    public class PackageCompatibilityVerificationEnqueuer : IPackageCompatibilityVerificationEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<PackageCompatibilityValidationMessage> _packageCompatibilityVerificationSerializer;

        public PackageCompatibilityVerificationEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<PackageCompatibilityValidationMessage> packageCompatibilityVerificationSerializer)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _packageCompatibilityVerificationSerializer = packageCompatibilityVerificationSerializer ?? throw new ArgumentNullException(nameof(packageCompatibilityVerificationSerializer));
        }

        /// <summary>
        /// Kicks off the package verification process for the given request. Verification will begin when the
        /// <see cref="ValidationEntitiesContext"/> has a <see cref="ValidatorStatus"/> that matches the
        /// <see cref="IValidationRequest"/>'s validationId. Once verification completes, the <see cref="ValidatorStatus"/>'s
        /// State will be updated to "Succeeded" or "Failed".
        /// </summary>
        /// <param name="request">The request that details the package to be verified.</param>
        /// <returns>A task that will complete when the verification process has been queued.</returns>
        public Task EnqueueVerificationAsync(IValidationRequest request)
        {
            var brokeredMessage = _packageCompatibilityVerificationSerializer.Serialize(
                new PackageCompatibilityValidationMessage(request.PackageId, request.PackageVersion, new Uri(request.NupkgUrl), request.ValidationId));

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
