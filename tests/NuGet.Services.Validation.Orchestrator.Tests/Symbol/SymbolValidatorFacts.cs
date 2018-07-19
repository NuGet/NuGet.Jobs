﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.Symbols;

using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolValidatorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "1.2.3";
        private static readonly Guid ValidationId = new Guid("12345678-1234-1234-1234-123456789012");
        private const string NupkgUrl = "https://example/nuget.versioning/1.2.3/package.nupkg";

        public class TheGetStatusMethod : FactsBase
        {
            private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.NotStarted,
                ValidationStatus.Succeeded,
            };

            public TheGetStatusMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task ReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.SymbolValidator,
                        State = status,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act & Assert
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(status, actual.Status);
            }

            [Fact]
            public async Task DoesReturnValidatorIssues()
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.SymbolValidator,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.Unknown,
                                Data = "Unknown",
                            },
                        },
                    });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(1, actual.Issues.Count);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] startedValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.Succeeded,
            };

            public TheStartValidationAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(StartedValidationStatuses))]
            public async Task ReturnsPersistedStatusesIfValidationAlreadyStarted(ValidationStatus status)
            {
                // Arrange
                _validatorStateService
                     .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                     .ReturnsAsync(new ValidatorStatus
                     {
                         ValidationId = ValidationId,
                         PackageKey = PackageKey,
                         ValidatorName = ValidatorName.SymbolValidator,
                         State = status,
                         ValidatorIssues = new List<ValidatorIssue>(),
                     });

                // Act & Assert
                await _target.StartAsync(_validationRequest.Object);

                _symbolMessageEnqueuer
                    .Verify(x => x.EnqueueSymbolsValidationMessageAsync(It.IsAny<IValidationRequest>()), Times.Never);

                _validatorStateService
                    .Verify(x => x.TryAddValidatorStatusAsync(It.IsAny<ValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);

                _telemetryService.Verify(
                    x => x.TrackValidatorEnquedMessage(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()),
                    Times.Never);
            }

            [Fact]
            public async Task StartsValidationIfNotStarted()
            {
                // Arrange
                // The order of operations is important! The state MUST be persisted AFTER verification has been queued.
                var statePersisted = false;
                bool verificationQueuedBeforeStatePersisted = false;

                _validatorStateService
                     .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                     .ReturnsAsync(new ValidatorStatus
                     {
                         ValidationId = ValidationId,
                         PackageKey = PackageKey,
                         ValidatorName = ValidatorName.SymbolValidator,
                         State = ValidationStatus.NotStarted,
                         ValidatorIssues = new List<ValidatorIssue>(),
                     });

                _symbolMessageEnqueuer
                    .Setup(x => x.EnqueueSymbolsValidationMessageAsync(It.IsAny<IValidationRequest>()))
                    .Callback(() =>
                    {
                        verificationQueuedBeforeStatePersisted = !statePersisted;
                    })
                    .Returns(Task.FromResult(0));

                _validatorStateService
                    .Setup(x => x.TryAddValidatorStatusAsync(
                                    It.IsAny<IValidationRequest>(),
                                    It.IsAny<ValidatorStatus>(),
                                    It.IsAny<ValidationStatus>()))
                    .Callback(() =>
                    {
                        statePersisted = true;
                    })
                    .ReturnsAsync(new ValidatorStatus
                    {
                        State = ValidationStatus.Incomplete,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    });

                // Act
                await _target.StartAsync(_validationRequest.Object);

                // Assert
                _symbolMessageEnqueuer
                    .Verify(x => x.EnqueueSymbolsValidationMessageAsync(It.IsAny<IValidationRequest>()), Times.Once);

                _validatorStateService
                    .Verify(
                        x => x.TryAddValidatorStatusAsync(
                                It.IsAny<IValidationRequest>(),
                                It.IsAny<ValidatorStatus>(),
                                It.Is<ValidationStatus>(s => s == ValidationStatus.Incomplete)),
                        Times.Once);

                _telemetryService.Verify(
                    x => x.TrackValidatorEnquedMessage( ValidatorName.SymbolValidator, _validationRequest.Object.ValidationId, It.IsAny<DateTime>()),
                    Times.Once);

                Assert.True(verificationQueuedBeforeStatePersisted);
            }

            [Fact]
            public async Task DoesReturnValidatorIssues()
            {
                // Arrange
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<IValidationRequest>()))
                    .ReturnsAsync(new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = ValidatorName.SymbolValidator,
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.Unknown,
                                Data = "Unknown",
                            },
                        },
                    });

                // Act
                var actual = await _target.StartAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(1, actual.Issues.Count);
            }

            public static IEnumerable<object[]> StartedValidationStatuses => startedValidationStatuses.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidatorStateService> _validatorStateService;
            protected readonly Mock<ISymbolMessageEnqueuer> _symbolMessageEnqueuer;
            protected readonly Mock<ISimpleCloudBlobProvider> _blobProvider;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly ILogger<SymbolValidator> _logger;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly SymbolValidator _target;

            protected readonly SymbolValidationConfiguration _config;

            public FactsBase(ITestOutputHelper output)
            {
                _validatorStateService = new Mock<IValidatorStateService>();
                _symbolMessageEnqueuer = new Mock<ISymbolMessageEnqueuer>();
                _blobProvider = new Mock<ISimpleCloudBlobProvider>();
                _config = new SymbolValidationConfiguration();
                _telemetryService = new Mock<ITelemetryService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<SymbolValidator>();

                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _target = new SymbolValidator(
                    _validatorStateService.Object,
                    _symbolMessageEnqueuer.Object,
                    _telemetryService.Object,
                    _logger);
            }
        }
    }
}
