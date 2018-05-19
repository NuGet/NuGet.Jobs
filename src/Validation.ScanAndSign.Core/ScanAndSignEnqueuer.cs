﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignEnqueuer : IScanAndSignEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IBrokeredMessageSerializer<ScanAndSignMessage> _serializer;
        private readonly ScanAndSignEnqueuerConfiguration _configuration;

        public ScanAndSignEnqueuer(
            ITopicClient topicClient,
            IBrokeredMessageSerializer<ScanAndSignMessage> serializer,
            IOptionsSnapshot<ScanAndSignEnqueuerConfiguration> configurationAccessor)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            if (configurationAccessor.Value == null)
            {
                throw new ArgumentException($"{nameof(configurationAccessor.Value)} property is null", nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value;
        }

        public Task EnqueueScanAsync(Guid validationId, string nupkgUrl)
        {
            if (nupkgUrl == null)
            {
                throw new ArgumentNullException(nameof(nupkgUrl));
            }

            var message = new ScanAndSignMessage(
                OperationRequestType.Scan,
                validationId,
                new Uri(nupkgUrl));
            var brokeredMessage = _serializer.Serialize(message);

            var visibleAt = DateTimeOffset.UtcNow + (_configuration.MessageDelay ?? TimeSpan.Zero);
            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAt;

            return _topicClient.SendAsync(brokeredMessage);
        }
    }
}
