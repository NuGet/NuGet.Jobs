﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageSigningValidatorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private const string NupkgUrl = "https://example/nuget.versioning/4.3.0/package.nupkg";

        public class TheGetStatusMethod : FactsBase
        {
            private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Failed,
                ValidationStatus.Incomplete,
                ValidationStatus.NotStarted,
                ValidationStatus.Succeeded,
            };

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task ReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatus(It.IsAny<IValidationRequest>()))
                    .Returns(status);

                // Act & Assert
                var actual = await _target.GetStatusAsync(_validationRequest.Object);

                Assert.Equal(status, actual);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] startedValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Failed,
                ValidationStatus.Incomplete,
                ValidationStatus.Succeeded,
            };

            [Theory]
            [MemberData(nameof(StartedValidationStatuses))]
            public async Task ThrowsIfValidationAlreadyStarted(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                     .Setup(x => x.GetStatus(It.IsAny<IValidationRequest>()))
                     .Returns(status);

                // Act & Assert
                // TODO: More specific exception!
                await Assert.ThrowsAsync<Exception>(() => _target.StartValidationAsync(_validationRequest.Object));
            }

            [Fact]
            public async Task StartsValidationIfNotStarted()
            {
                // Arrange
                // The order of operations is important! The state MUST be persisted AFTER verification has been queued.
                var statePersisted = false;
                bool verificationQueuedBeforeStatePersisted = false;

                _validatorStateService
                     .Setup(x => x.GetStatus(It.IsAny<IValidationRequest>()))
                     .Returns(ValidationStatus.NotStarted);

                _packageSignatureVerifier
                    .Setup(x => x.StartVerificationAsync(It.IsAny<IValidationRequest>()))
                    .Callback(() =>
                    {
                        verificationQueuedBeforeStatePersisted = !statePersisted;
                    })
                    .Returns(Task.FromResult(0));

                _validatorStateService
                    .Setup(x => x.AddStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidationStatus>()))
                    .Callback(() =>
                    {
                        statePersisted = true;
                    })
                    .Returns(Task.FromResult(0));

                // Act
                var actualStatus = await _target.StartValidationAsync(_validationRequest.Object);

                // Assert
                _packageSignatureVerifier
                    .Verify(x => x.StartVerificationAsync(It.IsAny<IValidationRequest>()), Times.Once);

                _validatorStateService
                    .Verify(
                        x => x.AddStatusAsync(
                                It.IsAny<IValidationRequest>(),
                                It.Is<ValidationStatus>(s => s == ValidationStatus.Incomplete)),
                        Times.Once);

                Assert.True(verificationQueuedBeforeStatePersisted);
                Assert.Equal(ValidationStatus.Incomplete, actualStatus);
            }

            public static IEnumerable<object[]> StartedValidationStatuses => startedValidationStatuses.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidatorStateService<PackageSigningValidator>> _validatorStateService;
            protected readonly Mock<IPackageSignatureVerifier> _packageSignatureVerifier;
            protected readonly Mock<ILogger<PackageSigningValidator>> _logger;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly PackageSigningValidator _target;

            public FactsBase()
            {
                _validatorStateService = new Mock<IValidatorStateService<PackageSigningValidator>>();
                _packageSignatureVerifier = new Mock<IPackageSignatureVerifier>();
                _logger = new Mock<ILogger<PackageSigningValidator>>();

                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _target = new PackageSigningValidator(
                        _validatorStateService.Object,
                        _packageSignatureVerifier.Object,
                        _logger.Object);
            }
        }
    }
}
