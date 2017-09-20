// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class OrchestrationRunnerFacts
    {
        [Fact]
        public async Task StartsMessageProcessing()
        {
            var orchestratorMock = new Mock<IOrchestrator>();
            var optionsAccessorMock = new Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();

            optionsAccessorMock
                .SetupGet(o => o.Value)
                .Returns(new OrchestrationRunnerConfiguration
                {
                    ProcessRecycleInterval = TimeSpan.Zero,
                    ShutdownWaitInterval = TimeSpan.Zero
                });

            var runner = new OrchestrationRunner(orchestratorMock.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            orchestratorMock.Verify(o => o.StartProcessingMessagesAsync(), Times.Once());
        }

        [Fact]
        public async Task ShutsDownMessageProcessing()
        {
            var orchestratorMock = new Mock<IOrchestrator>();
            var optionsAccessorMock = new Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();

            optionsAccessorMock
                .SetupGet(o => o.Value)
                .Returns(new OrchestrationRunnerConfiguration
                {
                    ProcessRecycleInterval = TimeSpan.Zero,
                    ShutdownWaitInterval = TimeSpan.Zero
                });

            var startCalled = false;
            orchestratorMock
                .Setup(o => o.StartProcessingMessagesAsync())
                .Callback(() => startCalled = true)
                .Returns(Task.FromResult(0));

            orchestratorMock
                .Setup(o => o.StartShuttingDownAsync())
                .Callback(() => Assert.True(startCalled))
                .Returns(Task.FromResult(0));
            var runner = new OrchestrationRunner(orchestratorMock.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            orchestratorMock.Verify(o => o.StartShuttingDownAsync(), Times.Once());
        }

        [Fact]
        public async Task WaitsOrchestratorToShutDown()
        {
            var orchestratorMock = new Mock<IOrchestrator>();
            var optionsAccessorMock = new Mock<IOptionsSnapshot<OrchestrationRunnerConfiguration>>();
            var loggerMock = new Mock<ILogger<OrchestrationRunner>>();

            optionsAccessorMock
                .SetupGet(o => o.Value)
                .Returns(new OrchestrationRunnerConfiguration
                {
                    ProcessRecycleInterval = TimeSpan.Zero,
                    ShutdownWaitInterval = TimeSpan.FromSeconds(2)
                });

            int numberOfRequestsInProgress = 2;
            orchestratorMock
                .Setup(o => o.GetNumberOfRequestsInProgress())
                .Returns(() => numberOfRequestsInProgress--);

            var runner = new OrchestrationRunner(orchestratorMock.Object, optionsAccessorMock.Object, loggerMock.Object);
            await runner.RunOrchestrationAsync();

            orchestratorMock.Verify(o => o.GetNumberOfRequestsInProgress(), Times.Exactly(3));
        }
    }
}
