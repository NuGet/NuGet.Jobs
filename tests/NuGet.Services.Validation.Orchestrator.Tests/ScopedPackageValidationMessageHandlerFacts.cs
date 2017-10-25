// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NuGet.Services.ServiceBus;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ScopedPackageValidationMessageHandlerFacts
    {
        [Fact]
        public async Task CreatesScopeUsesScopedServiceProviderPassesCallFurther()
        {
            var serviceScopeProviderMock = new Mock<IServiceScopeProvider>();
            var serviceScopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            var validationMessageHandlerMock = new Mock<IMessageHandler<PackageValidationMessageData>>();

            serviceScopeProviderMock
                .Setup(ssp => ssp.CreateScope())
                .Returns(serviceScopeMock.Object);

            serviceScopeMock
                .SetupGet(ss => ss.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IMessageHandler<PackageValidationMessageData>)))
                .Returns(validationMessageHandlerMock.Object);

            var handler = new ScopedPackageValidationMessageHandler(serviceScopeProviderMock.Object);
            var pvmd = new PackageValidationMessageData("SomePackageId", "1.2.3", Guid.NewGuid());
            await handler.HandleAsync(pvmd);

            serviceScopeProviderMock.Verify(ssp => ssp.CreateScope(), Times.Once());
            serviceProviderMock.Verify(sp => sp.GetService(typeof(IMessageHandler<PackageValidationMessageData>)), Times.Once());
            validationMessageHandlerMock.Verify(mh => mh.HandleAsync(pvmd), Times.Once());
        }
    }
}
