// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public class RevalidationService : IRevalidationService
    {
        private readonly IRevalidationStateService _state;
        private readonly IPackageValidationEnqueuer _enqueuer;

        public RevalidationService(IRevalidationStateService state, IPackageValidationEnqueuer enqueuer)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _enqueuer = enqueuer ?? throw new ArgumentNullException(nameof(enqueuer));
        }

        public async Task StartRevalidationAsync(PackageRevalidation revalidation)
        {
            var message = new PackageValidationMessageData(
                revalidation.PackageId,
                revalidation.PackageNormalizedVersion,
                revalidation.ValidationTrackingId.Value);

            await _enqueuer.StartValidationAsync(message);
        }
    }
}
