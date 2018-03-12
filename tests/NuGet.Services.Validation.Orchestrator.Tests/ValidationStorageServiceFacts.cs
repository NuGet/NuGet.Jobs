﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation.Issues;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationStorageServiceFacts
    {
        public class UpdateValidationSetStatusAsync : TelemetryFacts
        {
            public UpdateValidationSetStatusAsync(ITestOutputHelper output) : base(output)
            {
            }

            protected override async Task ExecuteAsync(ValidationResult validationResult)
            {
                await _target.UpdateValidationStatusAsync(_packageValidation, validationResult);
            }

            [Fact]
            public async Task UpdatesUpdateTimestamp()
            {
                // Arrange
                var originalUpdated = DateTime.UtcNow - TimeSpan.FromMinutes(5);

                var validationSet = new PackageValidationSet()
                {
                    Updated = originalUpdated
                };

                // Act & Assert
                await _target.UpdateValidationSetAsync(validationSet);

                _entitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.True(validationSet.Updated > originalUpdated);
            }
        }

        public class UpdateValidationStatusAsync : TelemetryFacts
        {
            public UpdateValidationStatusAsync(ITestOutputHelper output) : base(output)
            {
            }

            protected override async Task ExecuteAsync(ValidationResult validationResult)
            {
                await _target.UpdateValidationStatusAsync(_packageValidation, validationResult);
            }

            [Theory]
            [InlineData(ValidationStatus.Failed)]
            [InlineData(ValidationStatus.Succeeded)]
            [InlineData(ValidationStatus.Incomplete)]
            public async Task DoesNotEmitTelemtryForNoStatusChange(ValidationStatus status)
            {
                // Arrange
                _packageValidation.ValidationStatus = status;
                var validationResult = new ValidationResult(status);

                _telemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
                InitializeTarget();

                // Act
                await ExecuteAsync(validationResult);

                // Assert
                _telemetryService.VerifyAll();
            }
        }

        public class MarkValidationStartedAsync : TelemetryFacts
        {
            public MarkValidationStartedAsync(ITestOutputHelper output) : base(output)
            {
            }

            protected override async Task ExecuteAsync(ValidationResult validationResult)
            {
                await _target.MarkValidationStartedAsync(_packageValidation, validationResult);
            }
        }

        public class GetValidationSetCountAsync : Facts
        {
            public GetValidationSetCountAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsZeroWhenThereAreNoMatchingValidationSets()
            {
                // Arrange & Act
                var actual = await _target.GetValidationSetCountAsync(_packageKey);

                // Assert
                Assert.Equal(0, actual);
            }

            [Fact]
            public async Task ReturnsMatchingCount()
            {
                // Arrange
                _entitiesContext
                    .Setup(x => x.PackageValidationSets)
                    .Returns(DbSetMockFactory.Create(
                        new PackageValidationSet { PackageKey = _packageKey - 1 },
                        new PackageValidationSet { PackageKey = _packageKey },
                        new PackageValidationSet { PackageKey = _packageKey },
                        new PackageValidationSet { PackageKey = _packageKey },
                        new PackageValidationSet { PackageKey = _packageKey + 1 }));

                var actual = await _target.GetValidationSetCountAsync(_packageKey);

                Assert.Equal(3, actual);
            }
        }

        public class OtherRecentValidationSetForPackageExists : Facts
        {
            private readonly DateTime _now;
            private readonly TimeSpan _duration;
            private readonly DateTime _before;
            private readonly Guid _idA;
            private readonly Guid _idB;

            public OtherRecentValidationSetForPackageExists(ITestOutputHelper output) : base(output)
            {
                _now = DateTime.UtcNow;
                _duration = TimeSpan.FromHours(1);
                _before = _now - _duration.Add(TimeSpan.FromSeconds(1));
                _idA = new Guid("925bbbd0-bb0e-4a74-9e4f-563645e14398");
                _idB = new Guid("2dac1fd6-3d46-405a-9107-12ff230b0500");
            }

            [Fact]
            public async Task ReturnsTrueIfOtherValidationSetIsInRange()
            {
                // Arrange
                _entitiesContext
                    .Setup(x => x.PackageValidationSets)
                    .Returns(DbSetMockFactory.Create(
                        new PackageValidationSet { PackageKey = _packageKey, Created = _now, ValidationTrackingId = _idB }));

                // Act
                var actual = await _target.OtherRecentValidationSetForPackageExists(
                    _packageKey,
                    _duration,
                    _idA);

                // Assert
                Assert.True(actual, $"The validation set with validation ID {_idB} should have been detected.");
            }

            [Fact]
            public async Task ReturnsFalseIfOnlyValidationSetInRangeIsSelf()
            {
                // Arrange
                _entitiesContext
                    .Setup(x => x.PackageValidationSets)
                    .Returns(DbSetMockFactory.Create(
                        new PackageValidationSet { PackageKey = _packageKey, Created = _now, ValidationTrackingId = _idA }));

                // Act
                var actual = await _target.OtherRecentValidationSetForPackageExists(
                    _packageKey,
                    _duration,
                    _idA);

                // Assert
                Assert.False(actual, $"The current validation set with ID {_idA} should not have be considered.");
            }

            [Fact]
            public async Task ReturnsFalseIfOtherValidationSetIsOutOfRange()
            {
                // Arrange
                _entitiesContext
                    .Setup(x => x.PackageValidationSets)
                    .Returns(DbSetMockFactory.Create(
                        new PackageValidationSet { PackageKey = _packageKey, Created = _before, ValidationTrackingId = _idB }));

                // Act
                var actual = await _target.OtherRecentValidationSetForPackageExists(
                    _packageKey,
                    _duration,
                    _idA);

                // Assert
                Assert.False(actual, $"The validation set with ID {_idB} should not have be considered since it was out of the range.");
            }
        }

        public abstract class TelemetryFacts : Facts
        {
            public TelemetryFacts(ITestOutputHelper output) : base(output)
            {
            }

            protected abstract Task ExecuteAsync(ValidationResult validationResult);

            [Theory]
            [InlineData(ValidationStatus.Failed, false)]
            [InlineData(ValidationStatus.Succeeded, true)]
            public async Task EmitsTelemetryWhenStatusChanges(ValidationStatus toStatus, bool isSuccess)
            {
                // Arrange
                TimeSpan duration = default(TimeSpan);
                _telemetryService
                    .Setup(x => x.TrackValidatorDuration(It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<bool>()))
                    .Callback<TimeSpan, string, bool>((d, _, __) => duration = d);

                var validationResult = new ValidationResult(
                    toStatus,
                    new List<IValidationIssue>
                    {
                        ValidationIssue.PackageIsSigned,
                        new ClientSigningVerificationFailure("NU3000", "Issue A"),
                        new ClientSigningVerificationFailure("NU3001", "Issue B"),
                    });

                // Act
                var before = DateTime.UtcNow;
                await ExecuteAsync(validationResult);
                var after = DateTime.UtcNow;

                // Assert
                _telemetryService.Verify(
                    x => x.TrackValidatorDuration(It.IsAny<TimeSpan>(), _validatorType, isSuccess),
                    Times.Once);
                Assert.InRange(duration, before - _packageValidation.Started.Value, after - _packageValidation.Started.Value);

                _telemetryService.Verify(x => x.TrackValidationIssueCount(3, _validatorType, isSuccess), Times.Once);
                _telemetryService.Verify(x => x.TrackValidationIssue(_validatorType, ValidationIssueCode.PackageIsSigned));
                _telemetryService.Verify(x => x.TrackValidationIssue(_validatorType, ValidationIssueCode.ClientSigningVerificationFailure));
                _telemetryService.Verify(x => x.TrackValidationIssue(_validatorType, ValidationIssueCode.ClientSigningVerificationFailure));
                _telemetryService.Verify(x => x.TrackClientValidationIssue(_validatorType, "NU3000"), Times.Once);
                _telemetryService.Verify(x => x.TrackClientValidationIssue(_validatorType, "NU3001"), Times.Once);
            }

            [Fact]
            public async Task EmitsZeroDurationIfStartedPropertyIsNotSet()
            {
                // Arrange
                _packageValidation.Started = null;

                var validationResult = new ValidationResult(ValidationStatus.Failed);

                // Act
                await ExecuteAsync(validationResult);

                // Assert
                _telemetryService.Verify(
                    x => x.TrackValidatorDuration(TimeSpan.Zero, _validatorType, false),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotEmitTelemtryForIncomplete()
            {
                // Arrange
                _packageValidation.ValidationStatus = ValidationStatus.NotStarted;
                var validationResult = new ValidationResult(ValidationStatus.Incomplete);

                _telemetryService = new Mock<ITelemetryService>(MockBehavior.Strict);
                InitializeTarget();

                // Act
                await ExecuteAsync(validationResult);

                // Assert
                _telemetryService.VerifyAll();
            }

            [Theory]
            [InlineData(ValidationStatus.Failed)]
            [InlineData(ValidationStatus.Incomplete)]
            [InlineData(ValidationStatus.Succeeded)]
            public async Task DoesNotThrowWhenPackageValidationIssuesIsNull(ValidationStatus validationStatus)
            {
                // Arrange
                _packageValidation.PackageValidationIssues = null;
                var validationResult = new ValidationResult(validationStatus);

                // Act
                var ex = await Record.ExceptionAsync(async() => await ExecuteAsync(validationResult));

                // Assert
                Assert.Null(ex);
            }
        }

        public abstract class Facts
        {
            protected int _packageKey;
            protected string _validatorType;
            protected PackageValidation _packageValidation;
            protected Mock<IValidationEntitiesContext> _entitiesContext;
            protected Mock<ITelemetryService> _telemetryService;
            protected LoggerFactory _loggerFactory;
            protected ValidationStorageService _target;

            public Facts(ITestOutputHelper output)
            {
                _packageKey = 23;

                _validatorType = "ExampleValidator";
                _packageValidation = new PackageValidation
                {
                    Type = _validatorType,
                    ValidationStatus = ValidationStatus.Incomplete,
                    Started = new DateTime(2017, 1, 1, 8, 30, 0, DateTimeKind.Utc),
                    PackageValidationIssues = new List<PackageValidationIssue>(),
                    PackageValidationSet = new PackageValidationSet(),
                };

                _entitiesContext = new Mock<IValidationEntitiesContext>();
                _entitiesContext
                    .Setup(x => x.PackageValidationSets)
                    .Returns(DbSetMockFactory.Create<PackageValidationSet>());

                _telemetryService = new Mock<ITelemetryService>();

                _loggerFactory = new LoggerFactory();
                _loggerFactory.AddXunit(output);

                InitializeTarget();
            }

            protected void InitializeTarget()
            {
                _target = new ValidationStorageService(
                    _entitiesContext.Object,
                    _telemetryService.Object,
                    _loggerFactory.CreateLogger<ValidationStorageService>());
            }
        }
    }
}
