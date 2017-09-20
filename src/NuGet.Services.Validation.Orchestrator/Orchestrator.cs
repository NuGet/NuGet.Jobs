// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Receives validation messages, deserializes them and passes them down to the handler
    /// </summary>
    public class Orchestrator : IOrchestrator
    {
        private readonly ISubscriptionClient _subscriptionClient;
        private readonly IValidationMessageHandler _validationMessageHandler;
        private readonly IServiceBusMessageSerializer _serviceBusMessageSerializer;
        private readonly ILogger<Orchestrator> _logger;

        private int _numberOfRequestsInProgress;

        public Orchestrator(
            ISubscriptionClient subscriptionClient,
            IValidationMessageHandler validationMessageHandler,
            IServiceBusMessageSerializer serviceBusMessageSerializer,
            ILogger<Orchestrator> logger)
        {
            _subscriptionClient = subscriptionClient ?? throw new ArgumentNullException(nameof(subscriptionClient));
            _validationMessageHandler = validationMessageHandler ?? throw new ArgumentNullException(nameof(validationMessageHandler));
            _serviceBusMessageSerializer = serviceBusMessageSerializer ?? throw new ArgumentNullException(nameof(serviceBusMessageSerializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _numberOfRequestsInProgress = 0;
        }

        public Task StartProcessingMessagesAsync()
        {
            _subscriptionClient.OnMessageAsync(OnMessageAsync);
            return Task.FromResult(0);
        }

        public Task StartShuttingDownAsync()
        {
            return _subscriptionClient.CloseAsync();
        }

        public int GetNumberOfRequestsInProgress()
        {
            return _numberOfRequestsInProgress;
        }

        private async Task OnMessageAsync(IBrokeredMessage message)
        {
            Interlocked.Increment(ref _numberOfRequestsInProgress);
            try
            {
                var messageData = _serviceBusMessageSerializer.DeserializePackageValidationMessageData(message);
                _logger.LogInformation("Received message about validation set {ValidationSetId}, package: {PackageId} {PackageVersion}",
                    messageData.ValidationTrackingId,
                    messageData.PackageId,
                    messageData.PackageVersion);

                await _validationMessageHandler.OnMessageAsync(messageData);

                await message.CompleteAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(Error.OrchestratorOnMessageException, e, "Failed to process message");
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _numberOfRequestsInProgress);
            }
        }
    }
}
