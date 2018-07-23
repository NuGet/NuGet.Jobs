﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    public class RevalidationSharedStateService : IRevalidationSharedStateService
    {
        private readonly NuGetGallery.IRevalidationStateService _state;
        private readonly RevalidationConfiguration _config;
        private readonly ILogger<RevalidationSharedStateService> _logger;

        public RevalidationSharedStateService(
            NuGetGallery.IRevalidationStateService state,
            RevalidationConfiguration config,
            ILogger<RevalidationSharedStateService> logger)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsInitializedAsync()
        {
            return await GetStateValue(s => s.IsInitialized);
        }

        public async Task MarkAsInitializedAsync()
        {
            _logger.LogInformation("Updating state as initialized");

            await _state.UpdateStateAsync(s => s.IsInitialized = true);
        }

        public async Task<bool> IsKillswitchActiveAsync()
        {
            return await GetStateValue(s => s.IsKillswitchActive);
        }

        public async Task<int> GetDesiredPackageEventRateAsync()
        {
            // Ensure the desired rate is within the configured lower and upper bounds. A desired rate that's outside
            // the bounds indicates that the job was redeployed with different configuration values.
            var finalState = await _state.MaybeUpdateStateAsync(state =>
            {
                if (state.DesiredPackageEventRate < _config.MinPackageEventRate)
                {
                    _logger.LogInformation(
                        "Overriding desired package event rate {ToRate} from {FromRate}",
                        _config.MinPackageEventRate,
                        state.DesiredPackageEventRate);

                    state.DesiredPackageEventRate = _config.MinPackageEventRate;

                    return true;
                }
                else if (state.DesiredPackageEventRate > _config.MaxPackageEventRate)
                {
                    _logger.LogInformation(
                        "Overriding desired package event rate {ToRate} from {FromRate}",
                        _config.MaxPackageEventRate,
                        state.DesiredPackageEventRate);

                    state.DesiredPackageEventRate = _config.MaxPackageEventRate;

                    return true;
                }

                // The rate is within the expected bounds. Don't update anything.
                return false;
            });

            return finalState.DesiredPackageEventRate;
        }

        public async Task ResetDesiredPackageEventRateAsync()
        {
            await _state.UpdateStateAsync(state =>
            {
                _logger.LogInformation(
                    "Resetting desired package event rate to {ToRate} from {FromRate}",
                    _config.MinPackageEventRate,
                    state.DesiredPackageEventRate);

                state.DesiredPackageEventRate = _config.MinPackageEventRate;
            });
        }

        public async Task IncreaseDesiredPackageEventRateAsync()
        {
            await _state.MaybeUpdateStateAsync(state =>
            {
                // Don't update the state if we've reached the upper limit.
                if (state.DesiredPackageEventRate == _config.MaxPackageEventRate)
                {
                    _logger.LogInformation(
                        "Desired package event rate is at configured maximum of {MaxRate} per hour",
                        _config.MaxPackageEventRate);

                    return false;
                }

                var nextRate = Math.Min(_config.MaxPackageEventRate, state.DesiredPackageEventRate + 1);

                _logger.LogInformation(
                    "Increasing desired package event rate to {ToRate} from {FromRate}",
                    nextRate,
                    state.DesiredPackageEventRate);

                state.DesiredPackageEventRate = nextRate;
                return true;
            });
        }

        private async Task<T> GetStateValue<T>(Func<RevalidationState, T> callback)
        {
            return callback(await _state.GetStateAsync());
        }
    }
}
