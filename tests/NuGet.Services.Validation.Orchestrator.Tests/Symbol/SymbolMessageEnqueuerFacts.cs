// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using Moq;
using Xunit;


namespace NuGet.Services.Validation.Symbols
{
    public class SymbolMessageEnqueuerFacts
    {
        [Fact]
        public async Task SendsSerializeMessage()
        {
            SymbolValidatorMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SymbolValidatorMessage>(x => message = x);

            await _target.EnqueueSymbolMessageAsync(_validationRequest.Object);

            Assert.Equal(_validationRequest.Object.ValidationId, message.ValidationId);
            Assert.Equal(_validationRequest.Object.PackageId, message.PackageId);
            Assert.Equal(_validationRequest.Object.PackageVersion, message.PackageNormalizedVersion);
            Assert.Equal(_validationRequest.Object.NupkgUrl, message.SNupkgUrl);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<SymbolValidatorMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<SymbolValidatorMessage>> _serializer;
        private readonly Mock<IOptionsSnapshot<SymbolValidationConfiguration>> _options;
        private readonly SymbolValidationConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly Mock<IValidationRequest> _validationRequest;
        private readonly SymbolMessageEnqueuer _target;

        public SymbolMessageEnqueuerFacts()
        {
            _configuration = new SymbolValidationConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new Mock<IValidationRequest>();

            _validationRequest.Setup(x => x.ValidationId).Returns(new Guid("ab2629ce-2d67-403a-9a42-49748772ae90"));
            _validationRequest.Setup(x => x.PackageId).Returns("NuGet.Versioning");
            _validationRequest.Setup(x => x.PackageVersion).Returns("4.6.0");
            _validationRequest.Setup(x => x.NupkgUrl).Returns("http://example/nuget.versioning.4.6.0.nupkg?my-sas");
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();
            _serializer = new Mock<IBrokeredMessageSerializer<SymbolValidatorMessage>>();
            _options = new Mock<IOptionsSnapshot<SymbolValidationConfiguration>>();

            _options.Setup(x => x.Value).Returns(() => _configuration);
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object);

            _target = new SymbolMessageEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                _options.Object);
        }
    }
}
