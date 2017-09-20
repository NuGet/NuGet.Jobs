// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationSetProviderFacts
    {
        [Fact]
        public async Task TriesToGetValidationSetFirst()
        {
            var validationStorageMock = new Mock<IValidationStorageService>(MockBehavior.Strict);
            var configurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            var loggerMock = new Mock<ILogger<ValidationSetProvider>>();

            var configuration = new ValidationConfiguration();
            configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(configuration);

            var package = new Package();
            Guid validationTrackingId = Guid.NewGuid();
            var validationSet = new PackageValidationSet();

            validationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync(validationSet)
                .Verifiable();

            var provider = new ValidationSetProvider(
                validationStorageMock.Object,
                configurationAccessorMock.Object,
                loggerMock.Object);

            var set = await provider.GetOrCreateValidationSetAsync(validationTrackingId, package);

            validationStorageMock
                .Verify(vs => vs.GetValidationSetAsync(validationTrackingId), Times.Once());

            Assert.Same(validationSet, set);
        }

        [Fact]
        public async Task ProperlyConstructsValidationSet()
        {
            var validationStorageMock = new Mock<IValidationStorageService>(MockBehavior.Strict);
            var configurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            var loggerMock = new Mock<ILogger<ValidationSetProvider>>();

            var configuration = new ValidationConfiguration();
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, FailAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ validation2 } },
                new ValidationConfigurationItem(){ Name = validation2, FailAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };
            configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(configuration);

            var package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package1" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                Key = 42,
            };
            package.PackageRegistration.Packages = new List<Package> { package };

            Guid validationTrackingId = Guid.NewGuid();
            validationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync(null)
                .Verifiable();

            PackageValidationSet createdSet = null;
            validationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            var provider = new ValidationSetProvider(
                validationStorageMock.Object,
                configurationAccessorMock.Object,
                loggerMock.Object);

            var returnedSet = await provider.GetOrCreateValidationSetAsync(validationTrackingId, package);
            var endOfCallTimestamp = DateTime.UtcNow;

            validationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            Assert.NotNull(returnedSet);
            Assert.NotNull(createdSet);
            Assert.Same(createdSet, returnedSet);
            Assert.Equal(package.PackageRegistration.Id, createdSet.PackageId);
            Assert.Equal(package.NormalizedVersion, createdSet.PackageNormalizedVersion);
            Assert.Equal(package.Key, createdSet.PackageKey);
            Assert.Equal(validationTrackingId, createdSet.ValidationTrackingId);
            Assert.True(createdSet.Created.Kind == DateTimeKind.Utc);
            Assert.True(createdSet.Updated.Kind == DateTimeKind.Utc);

            var allowedTimeDifference = TimeSpan.FromSeconds(5);
            Assert.True(endOfCallTimestamp - createdSet.Created < allowedTimeDifference);
            Assert.True(endOfCallTimestamp - createdSet.Updated < allowedTimeDifference);
            Assert.All(createdSet.PackageValidations, v => Assert.Same(createdSet, v.PackageValidationSet));
            Assert.All(createdSet.PackageValidations, v => Assert.Equal(ValidationStatus.NotStarted, v.ValidationStatus));
            Assert.All(createdSet.PackageValidations, v => Assert.True(endOfCallTimestamp - v.ValidationStatusTimestamp < allowedTimeDifference));
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation1);
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation2);
        }
    }
}
