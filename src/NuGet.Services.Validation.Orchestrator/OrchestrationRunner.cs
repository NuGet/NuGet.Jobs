// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Controls the lifetime and proper shutdown of the Orchestrator instance
    /// </summary>
    public class OrchestrationRunner
    {
        private readonly IOrchestrator _orchestrator;
        private readonly OrchestrationRunnerConfiguration _configuration;
        private readonly ILogger<OrchestrationRunner> _logger;

        public OrchestrationRunner(
            IOrchestrator orchestrator,
            IOptionsSnapshot<OrchestrationRunnerConfiguration> configurationAccessor,
            ILogger<OrchestrationRunner> logger)
        {
            _orchestrator = orchestrator  ?? throw new ArgumentNullException(nameof(orchestrator));
            configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));
            _configuration = configurationAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunOrchestrationAsync()
        {
            _logger.LogInformation("Starting up the orchestration");

            await _orchestrator.StartProcessingMessagesAsync();
            await Task.Delay(_configuration.ProcessRecycleInterval);

            _logger.LogInformation("Recycling the process...");
            await _orchestrator.StartShuttingDownAsync();

            DateTimeOffset waitStart = DateTimeOffset.Now;

            while (DateTimeOffset.Now - waitStart < _configuration.ShutdownWaitInterval)
            {
                if (_orchestrator.GetNumberOfRequestsInProgress() <= 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            int stillRunning = _orchestrator.GetNumberOfRequestsInProgress();

            if (stillRunning > 0)
            {
                _logger.LogWarning("There are still {StillRunningRequests} requests running after requesting shutdown and waiting for {ShutdownWaitInterval}",
                    stillRunning,
                    _configuration.ShutdownWaitInterval);
            }
            else
            {
                _logger.LogInformation("All requests are finished");
            }
        }
    }
}
