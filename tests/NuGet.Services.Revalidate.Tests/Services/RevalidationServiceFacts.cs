﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class RevalidationServiceFacts
    {
        public class TheRunAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsUnrecoverableError()
            {
                /// This test is a sanity check to ensure <see cref="OnUnrecoverableError_ShutsDown" /> works as expected.
                SetupUnrecoverableErrorResult();

                Assert.Equal(RevalidationResult.UnrecoverableError, await _target.StartNextRevalidationAsync());
            }

            [Fact]
            public async Task OnUnrecoverableError_ShutsDown()
            {
                // Arrange
                // Configure the job to run for a very long time. The unrecoverable error causes the job to end after its first iteration.
                _config.ShutdownWaitInterval = TimeSpan.MaxValue;

                SetupUnrecoverableErrorResult();

                // Act & Assert
                await _target.RunAsync();

                _singletonService.Verify(s => s.IsSingletonAsync(), Times.Once);
            }

            [Fact]
            public async Task ReturnsRetryLater()
            {
                /// This test is a sanity check to ensure <see cref="OnUnrecoverableError_ShutsDown" /> works as expected.
                SetupRetryLaterResult();

                Assert.Equal(RevalidationResult.RetryLater, await _target.StartNextRevalidationAsync());
            }

            [Fact]
            public async Task OnRetryLater_CallsThrottlerCallback()
            {
                // Arrange
                SetupRetryLaterResult();

                // Act & Assert
                await _target.RunAsync();

                _throttler.Verify(t => t.OnRetryRevalidationLaterAsync(), Times.Once);
            }

            [Fact]
            public async Task OnRevalidationEnqueued_CallsThrottlerCallback()
            {
                // Arrange
                Setup(next: _revalidation);

                // Act & Assert
                await _target.RunAsync();

                _throttler.Verify(t => t.OnRevalidationEnqueuedAsync(), Times.Once);
            }
        }

        public class TheStartNextRevalidationAsyncMethod : FactsBase
        {
            [Fact]
            public async Task IfNotSingleton_ReturnsUnrecoverableError()
            {
                // Arrange
                Setup(isSingleton: false);

                // Act & Assert
                var result = await _target.StartNextRevalidationAsync();

                _singletonService.Verify(s => s.IsSingletonAsync(), Times.Once);
                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Never);
                _validationEnqueuer.Verify(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);

                Assert.Equal(RevalidationResult.UnrecoverableError, result);
            }

            [Fact]
            public async Task IfKillswitchActive_ReturnsRetryLater()
            {
                // Arrange
                Setup(killswitchActive: true);

                // Act & Assert
                var result = await _target.StartNextRevalidationAsync();

                _stateService.Verify(s => s.IsKillswitchActiveAsync(), Times.Once);
                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Never);
                _validationEnqueuer.Verify(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);

                Assert.Equal(RevalidationResult.RetryLater, result);
            }

            [Fact]
            public async Task IfThrottled_ReturnsRetryLater()
            {
                // Arrange
                Setup(isThrottled: true);

                // Act & Assert
                var result = await _target.StartNextRevalidationAsync();

                _throttler.Verify(s => s.IsThrottledAsync(), Times.Once);
                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Never);
                _validationEnqueuer.Verify(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);

                Assert.Equal(RevalidationResult.RetryLater, result);
            }

            [Fact]
            public async Task IfUnhealthy_ReturnsRetryLater()
            {
                // Arrange
                Setup(isHealthy: false);

                // Act & Assert
                var result = await _target.StartNextRevalidationAsync();

                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Never);
                _healthService.Verify(h => h.IsHealthyAsync(), Times.Once);
                _validationEnqueuer.Verify(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);

                Assert.Equal(RevalidationResult.RetryLater, result);
            }

            [Fact]
            public async Task IfRevalidationQueueEmpty_ReturnsRetryLater()
            {
                // Arrange
                Setup(next: null);

                // Act & Assert
                var result = await _target.StartNextRevalidationAsync();

                _singletonService.Verify(s => s.IsSingletonAsync(), Times.Once);
                _stateService.Verify(s => s.IsKillswitchActiveAsync(), Times.Exactly(2));
                _throttler.Verify(s => s.IsThrottledAsync(), Times.Once);
                _healthService.Verify(h => h.IsHealthyAsync(), Times.Once);

                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Once);

                _validationEnqueuer.Verify(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()), Times.Never);

                Assert.Equal(RevalidationResult.RetryLater, result);
            }

            [Fact]
            public async Task StartsNextRevalidation()
            {
                // Arrange
                Setup(next: _revalidation);

                var order = 0;
                int enqueueStep = 0;
                int markStep = 0;

                _validationEnqueuer
                    .Setup(e => e.StartValidationAsync(It.IsAny<PackageValidationMessageData>()))
                    .Callback(() => enqueueStep = order++)
                    .Returns(Task.CompletedTask);

                _stateService
                    .Setup(s => s.MarkRevalidationAsEnqueuedAsync(It.IsAny<PackageRevalidation>()))
                    .Callback(() => markStep = order++)
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _target.StartNextRevalidationAsync();

                // Assert
                _singletonService.Verify(s => s.IsSingletonAsync(), Times.Once);
                _stateService.Verify(s => s.IsKillswitchActiveAsync(), Times.Exactly(2));
                _throttler.Verify(s => s.IsThrottledAsync(), Times.Once);
                _healthService.Verify(h => h.IsHealthyAsync(), Times.Once);

                _throttler.Verify(t => t.IncreaseCapacityAsync(), Times.Once);

                _validationEnqueuer.Verify(
                    e => e.StartValidationAsync(It.Is<PackageValidationMessageData>(m =>
                        m.PackageId == _revalidation.PackageId &&
                        m.PackageNormalizedVersion == _revalidation.PackageNormalizedVersion &&
                        m.ValidationTrackingId == _revalidation.ValidationTrackingId.Value)),
                    Times.Once);

                _stateService.Verify(s => s.MarkRevalidationAsEnqueuedAsync(_revalidation), Times.Once);
                _telemetryService.Verify(t => t.TrackPackageRevalidationStarted(_revalidation.PackageId, _revalidation.PackageNormalizedVersion));

                Assert.Equal(RevalidationResult.RevalidationEnqueued, result);
                Assert.Equal(2, order);
                Assert.True(enqueueStep < markStep);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IRevalidationStateService> _stateService;
            protected readonly Mock<ISingletonService> _singletonService;
            protected readonly Mock<IRevalidationThrottler> _throttler;
            protected readonly Mock<IHealthService> _healthService;
            protected readonly Mock<IRevalidationQueue> _revalidationQueue;
            protected readonly Mock<IPackageValidationEnqueuer> _validationEnqueuer;
            protected readonly Mock<ITelemetryService> _telemetryService;

            protected readonly RevalidationConfiguration _config;
            protected readonly PackageRevalidation _revalidation;

            public RevalidationService _target;

            public FactsBase()
            {
                _stateService = new Mock<IRevalidationStateService>();
                _singletonService = new Mock<ISingletonService>();
                _throttler = new Mock<IRevalidationThrottler>();
                _healthService = new Mock<IHealthService>();
                _revalidationQueue = new Mock<IRevalidationQueue>();
                _validationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                _telemetryService = new Mock<ITelemetryService>();

                _config = new RevalidationConfiguration
                {
                    ShutdownWaitInterval = TimeSpan.MinValue,
                };

                _revalidation = new PackageRevalidation
                {
                    PackageId = "Foo.Bar",
                    PackageNormalizedVersion = "1.2.3",
                    ValidationTrackingId = Guid.NewGuid()
                };

                _target = new RevalidationService(
                    _stateService.Object,
                    _singletonService.Object,
                    _throttler.Object,
                    _healthService.Object,
                    _revalidationQueue.Object,
                    _validationEnqueuer.Object,
                    _config,
                    _telemetryService.Object,
                    Mock.Of<ILogger<RevalidationService>>());
            }

            protected void Setup(bool isSingleton = true, bool killswitchActive = false, bool isThrottled = false, bool isHealthy = true, PackageRevalidation next = null)
            {
                _singletonService.Setup(s => s.IsSingletonAsync()).ReturnsAsync(isSingleton);
                _stateService.Setup(s => s.IsKillswitchActiveAsync()).ReturnsAsync(killswitchActive);
                _throttler.Setup(t => t.IsThrottledAsync()).ReturnsAsync(isThrottled);
                _healthService.Setup(t => t.IsHealthyAsync()).ReturnsAsync(isHealthy);
                _revalidationQueue.Setup(q => q.NextOrNullAsync()).ReturnsAsync(next);
            }

            protected void SetupUnrecoverableErrorResult()
            {
                Setup(isSingleton: false);
            }

            protected void SetupRetryLaterResult()
            {
                Setup(killswitchActive: true, isThrottled: true, isHealthy: false);
            }
        }
    }
}
