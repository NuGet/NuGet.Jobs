// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class OrchestratorFacts
    {
        [Fact]
        public async Task SetsUpSubscriptionClientOnStartProcessing()
        {
            var subscriptionClientMock = new Mock<ISubscriptionClient>();
            var validationMessageHandlerMock = new Mock<IValidationMessageHandler>(MockBehavior.Strict);
            var serviceBusSerializerMock = new Mock<IServiceBusMessageSerializer>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger<Orchestrator>>();

            var orchestrator = new Orchestrator(subscriptionClientMock.Object, validationMessageHandlerMock.Object, serviceBusSerializerMock.Object, loggerMock.Object);
            await orchestrator.StartProcessingMessagesAsync();

            subscriptionClientMock.Verify(sc => sc.OnMessageAsync(It.IsAny<Func<IBrokeredMessage, Task>>()), Times.Once());
        }

        [Fact]
        public async Task ShutsDownSubscriptionClient()
        {
            var subscriptionClientMock = new Mock<ISubscriptionClient>();
            var validationMessageHandlerMock = new Mock<IValidationMessageHandler>(MockBehavior.Strict);
            var serviceBusSerializerMock = new Mock<IServiceBusMessageSerializer>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger<Orchestrator>>();

            var orchestrator = new Orchestrator(subscriptionClientMock.Object, validationMessageHandlerMock.Object, serviceBusSerializerMock.Object, loggerMock.Object);
            await orchestrator.StartShuttingDownAsync();

            subscriptionClientMock.Verify(sc => sc.CloseAsync(), Times.Once());
        }

        [Fact]
        public async Task CallsHandler()
        {
            var subscriptionClientMock = new Mock<ISubscriptionClient>(MockBehavior.Strict);
            var validationMessageHandlerMock = new Mock<IValidationMessageHandler>(MockBehavior.Strict);
            var serviceBusSerializerMock = new Mock<IServiceBusMessageSerializer>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger<Orchestrator>>();
            var brokeredMessageMock = new Mock<IBrokeredMessage>(MockBehavior.Strict);

            Func<IBrokeredMessage, Task> onMessage = null;
            subscriptionClientMock
                .Setup(sc => sc.OnMessageAsync(It.IsAny<Func<IBrokeredMessage, Task>>()))
                .Callback<Func<IBrokeredMessage, Task>>(callback => onMessage = callback)
                .Verifiable();

            var pvmd = new PackageValidationMessageData("PackageId", "1.2.3", Guid.NewGuid());
            serviceBusSerializerMock
                .Setup(sbs => sbs.DeserializePackageValidationMessageData(brokeredMessageMock.Object))
                .Returns(pvmd)
                .Verifiable();

            validationMessageHandlerMock
                .Setup(vmh => vmh.OnMessageAsync(pvmd))
                .Returns(Task.FromResult(0))
                .Verifiable();

            brokeredMessageMock
                .Setup(bm => bm.CompleteAsync())
                .Returns(Task.FromResult(0))
                .Verifiable();

            var orchestrator = new Orchestrator(subscriptionClientMock.Object, validationMessageHandlerMock.Object, serviceBusSerializerMock.Object, loggerMock.Object);
            await orchestrator.StartProcessingMessagesAsync();

            Assert.NotNull(onMessage);
            await onMessage(brokeredMessageMock.Object);

            serviceBusSerializerMock.Verify(sbs => sbs.DeserializePackageValidationMessageData(brokeredMessageMock.Object), Times.Once());
            validationMessageHandlerMock.Verify(vmh => vmh.OnMessageAsync(pvmd), Times.Once());
            brokeredMessageMock.Verify(bm => bm.CompleteAsync(), Times.Once());
        }
    }
}
