// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationMessageHandlerFacts
    {
        [Fact]
        public async Task WaitsForPackageAvailabilityInGalleryDB()
        {
            var mocks = new AllTheMocks(MockBehavior.Strict);
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            var validationConfiguration = new ValidationConfiguration();

            mocks.CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns<Package>(null)
                .Verifiable();

            var handler = mocks.CreateHandler();

            await handler.OnMessageAsync(messageData);

            mocks.CorePackageServiceMock.Verify(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion), Times.Once());
        }

        [Fact]
        public async Task MakesSureValidationSetExists()
        {
            var mocks = new AllTheMocks(MockBehavior.Loose);
            var package = new Package();
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            mocks.CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(package);

            var validationSet = new PackageValidationSet();
            mocks.ValidationSetProviderMock
                .Setup(vsp => vsp.GetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package))
                .ReturnsAsync(validationSet)
                .Verifiable();

            var handler = mocks.CreateHandler();
            await handler.OnMessageAsync(messageData);

            mocks.ValidationSetProviderMock
                .Verify(vsp => vsp.GetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package));
        }

        [Fact]
        public async Task CallsProcessValidations()
        {
            var mocks = new AllTheMocks(MockBehavior.Loose);
            var package = new Package();
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            mocks.CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(package);

            var validationSet = new PackageValidationSet();
            mocks.ValidationSetProviderMock
                .Setup(vsp => vsp.GetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package))
                .ReturnsAsync(validationSet);

            mocks.ValidationSetProcessorMock
                .Setup(vsp => vsp.ProcessValidationsAsync(validationSet, package))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var handler = mocks.CreateHandler();
            await handler.OnMessageAsync(messageData);

            mocks.ValidationSetProcessorMock
                .Verify(vsp => vsp.ProcessValidationsAsync(validationSet, package), Times.Once());
        }

        [Fact]
        public async Task CallsProcessValidationOutcome()
        {
            var mocks = new AllTheMocks(MockBehavior.Loose);
            var package = new Package();
            var messageData = new PackageValidationMessageData("packageId", "1.2.3", Guid.NewGuid());
            mocks.CorePackageServiceMock
                .Setup(ps => ps.FindPackageByIdAndVersionStrict(messageData.PackageId, messageData.PackageVersion))
                .Returns(package);

            var validationSet = new PackageValidationSet();
            mocks.ValidationSetProviderMock
                .Setup(vsp => vsp.GetOrCreateValidationSetAsync(messageData.ValidationTrackingId, package))
                .ReturnsAsync(validationSet);

            mocks.ValidationSetProcessorMock
                .Setup(vsp => vsp.ProcessValidationsAsync(validationSet, package))
                .Returns(Task.FromResult(0));

            mocks.ValidationOutcomeProcessorMock
                .Setup(vop => vop.ProcessValidationOutcomeAsync(validationSet, package))
                .Returns(Task.FromResult(0))
                .Verifiable();

            var handler = mocks.CreateHandler();
            await handler.OnMessageAsync(messageData);

            mocks.ValidationOutcomeProcessorMock
                .Verify(vop => vop.ProcessValidationOutcomeAsync(validationSet, package));
        }

        private class AllTheMocks
        {
            public Mock<ICorePackageService> CorePackageServiceMock { get; }
            public Mock<IValidationSetProvider> ValidationSetProviderMock { get; }
            public Mock<IValidationSetProcessor> ValidationSetProcessorMock { get; }
            public Mock<IValidationOutcomeProcessor> ValidationOutcomeProcessorMock { get; }
            public Mock<ILogger<ValidationMessageHandler>> LoggerMock { get; }
            public AllTheMocks(MockBehavior behavior)
            {
                CorePackageServiceMock = new Mock<ICorePackageService>(behavior);
                ValidationSetProviderMock = new Mock<IValidationSetProvider>(behavior);
                ValidationSetProcessorMock = new Mock<IValidationSetProcessor>(behavior);
                ValidationOutcomeProcessorMock = new Mock<IValidationOutcomeProcessor>(behavior);
                LoggerMock = new Mock<ILogger<ValidationMessageHandler>>(); // we generally don't care about how logger is called, so it's loose all the time
            }

            public ValidationMessageHandler CreateHandler()
            {
                return new ValidationMessageHandler(
                    CorePackageServiceMock.Object,
                    ValidationSetProviderMock.Object,
                    ValidationSetProcessorMock.Object,
                    ValidationOutcomeProcessorMock.Object,
                    LoggerMock.Object);
            }
        }
    }
}
