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
        private readonly ILogger<OrchestrationRunner> _logger;
        private readonly IOrchestrator _orchestrator;
        private readonly OrchestrationRunnerConfiguration _configuration;

        public OrchestrationRunner(ILogger<OrchestrationRunner> logger, IOrchestrator orchestrator, IOptions<OrchestrationRunnerConfiguration> configurationAccessor)
        {
            _logger = logger;
            _orchestrator = orchestrator;
            _configuration = configurationAccessor.Value;
        }

        public async Task RunOrchestrationAsync()
        {
            _logger.LogInformation("Starting up the orchestration");

            await _orchestrator.StartProcessingMessagesAsync();
            Thread.Sleep(_configuration.ProcessRecycleInterval);

            _logger.LogInformation("Recycling the process...");
            await _orchestrator.StartShuttingDownAsync();

            DateTimeOffset waitStart = DateTimeOffset.Now;

            while (DateTimeOffset.Now - waitStart < _configuration.ShutdownWaitInterval)
            {
                if (await _orchestrator.GetNumberOfRequestsInProgressAsync() <= 0)
                {
                    break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            int stillRunning = await _orchestrator.GetNumberOfRequestsInProgressAsync();

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
