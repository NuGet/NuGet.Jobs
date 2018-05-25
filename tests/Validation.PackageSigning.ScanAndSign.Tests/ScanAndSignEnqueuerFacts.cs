﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class TheScanAndSignEnqueuerConstructor : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public void ThrowsWhenTopicClientIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                null,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("topicClient", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenSerializerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                null,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("serializer", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                null,
                _logger));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenConfigurationAccessorValueIsNull()
        {
            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns((ScanAndSignConfiguration)null);

            var ex = Assert.Throws<ArgumentException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger));

            Assert.Equal("configurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenLoggerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                null));

            Assert.Equal("logger", ex.ParamName);
        }
    }

    public class TheEnqueueScanAsyncMethod : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAsync(null));
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            await _target.EnqueueScanAsync(_validationRequest);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ScanAndSignMessage>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationId, _capturedMessage.PackageValidationId);
            Assert.Equal(OperationRequestType.Scan, _capturedMessage.OperationRequestType);
            Assert.Equal(_validationRequest.NupkgUrl, _capturedMessage.BlobUri.AbsoluteUri);
            Assert.Null(_capturedMessage.V3ServiceIndexUrl);
            Assert.Null(_capturedMessage.Owners);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueScanAsync(_validationRequest);

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");
            await _target.EnqueueScanAsync(request);

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class TheEnqueueScanAndSignAsyncMethod : ScanAndSignEnqueuerFactsBase
    {
        [Fact]
        public async Task ThrowsWhenParametersAreMissing()
        {
            var ex1 = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAndSignAsync(null, _configuration.V3ServiceIndexUrl, _owners));
            var ex2 = await Assert.ThrowsAsync<ArgumentException>(() => _target.EnqueueScanAndSignAsync(_validationRequest, null, _owners));
            var ex3 = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.EnqueueScanAndSignAsync(_validationRequest, _configuration.V3ServiceIndexUrl, null));

            Assert.Equal("request", ex1.ParamName);
            Assert.Equal("v3ServiceIndexUrl", ex2.ParamName);
            Assert.Equal("owners", ex3.ParamName);
        }

        [Fact]
        public async Task PassesDataToSerializedMessage()
        {
            await _target.EnqueueScanAndSignAsync(_validationRequest, _configuration.V3ServiceIndexUrl, _owners);

            _serializerMock
                .Verify(s => s.Serialize(It.IsAny<ScanAndSignMessage>()), Times.Once);

            Assert.Equal(_validationRequest.ValidationId, _capturedMessage.PackageValidationId);
            Assert.Equal(OperationRequestType.Sign, _capturedMessage.OperationRequestType);
            Assert.Equal(_validationRequest.NupkgUrl, _capturedMessage.BlobUri.AbsoluteUri);
            Assert.Equal(_configuration.V3ServiceIndexUrl, _capturedMessage.V3ServiceIndexUrl);
            Assert.Equal(_owners, _capturedMessage.Owners);
        }

        [Fact]
        public async Task SetsEnqueueTime()
        {
            const int messageDelayDays = 137;
            _configuration.MessageDelay = TimeSpan.FromDays(messageDelayDays);

            await _target.EnqueueScanAndSignAsync(_validationRequest, _configuration.V3ServiceIndexUrl, _owners);

            Assert.Equal(messageDelayDays, (_serializedMessage.ScheduledEnqueueTimeUtc - DateTimeOffset.UtcNow).TotalDays, 0);
        }

        [Fact]
        public async Task SendsMessage()
        {
            await _target.EnqueueScanAndSignAsync(_validationRequest, _configuration.V3ServiceIndexUrl, _owners);

            Assert.Same(_serializedMessage, _capturedBrokeredMessage);
        }
    }

    public class ScanAndSignEnqueuerFactsBase
    {
        protected Mock<ITopicClient> _topicClientMock;
        protected Mock<IBrokeredMessageSerializer<ScanAndSignMessage>> _serializerMock;
        protected Mock<IOptionsSnapshot<ScanAndSignConfiguration>> _configurationAccessorMock;
        protected ILogger<ScanAndSignEnqueuer> _logger;
        protected ScanAndSignConfiguration _configuration;
        protected ScanAndSignEnqueuer _target;

        protected ScanAndSignMessage _capturedMessage;
        protected IBrokeredMessage _capturedBrokeredMessage;
        protected BrokeredMessageWrapper _serializedMessage;

        protected readonly IValidationRequest _validationRequest;
        protected readonly List<string> _owners;

        public ScanAndSignEnqueuerFactsBase()
        {
            _topicClientMock = new Mock<ITopicClient>();
            _serializerMock = new Mock<IBrokeredMessageSerializer<ScanAndSignMessage>>();
            _configurationAccessorMock = new Mock<IOptionsSnapshot<ScanAndSignConfiguration>>();
            _logger = Mock.Of<ILogger<ScanAndSignEnqueuer>>();

            _configuration = new ScanAndSignConfiguration();

            _configuration.V3ServiceIndexUrl = "http://awesome.v3/service/index.json";

            _configurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(_configuration);

            _target = new ScanAndSignEnqueuer(
                _topicClientMock.Object,
                _serializerMock.Object,
                _configurationAccessorMock.Object,
                _logger);

            _validationRequest = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "someversion", "https://example.com/testpackage.nupkg");
            _owners = new List<string> {"Billy", "Bob"};

            _serializedMessage = new BrokeredMessageWrapper("somedata");

            _serializerMock
                .Setup(s => s.Serialize(It.IsAny<ScanAndSignMessage>()))
                .Callback<ScanAndSignMessage>(m => _capturedMessage = m)
                .Returns(_serializedMessage);

            _topicClientMock
                .Setup(tc => tc.SendAsync(It.IsAny<IBrokeredMessage>()))
                .Callback<IBrokeredMessage>(m => _capturedBrokeredMessage = m)
                .Returns(Task.CompletedTask);
        }
    }
}
