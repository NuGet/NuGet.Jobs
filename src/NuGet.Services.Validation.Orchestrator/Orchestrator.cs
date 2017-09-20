// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Main orchestrator code lives here
    /// </summary>
    public class Orchestrator : IOrchestrator
    {
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ILogger<Orchestrator> _logger;
        private readonly ISubscriptionClient _subscriptionClient;
        private readonly IValidationProvider _validationProvider;

        public Orchestrator(
            IOptions<ValidationConfiguration> validationConfigurationAccessor,
            ISubscriptionClient subscriptionClient,
            ILogger<Orchestrator> logger,
            IValidationProvider validationProvider)
        {
            this._validationConfiguration = validationConfigurationAccessor.Value;
            this._logger = logger;
            this._subscriptionClient = subscriptionClient;
            this._validationProvider = validationProvider;
        }

        public Task StartProcessingMessagesAsync()
        {
            _subscriptionClient.OnMessageAsync(this.OnMessageAsync);
            return Task.FromResult(0);
        }

        public Task StartShuttingDownAsync()
        {
            return _subscriptionClient.CloseAsync();
        }

        public Task<int> GetNumberOfRequestsInProgressAsync()
        {
            return Task.FromResult(0);
        }

        private Task OnMessageAsync(IBrokeredMessage message)
        {
            return Task.FromResult(0);
        }
    }
}
