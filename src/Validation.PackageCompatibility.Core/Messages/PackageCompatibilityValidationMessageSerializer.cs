// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;
using System;

namespace Validation.PackageCompatibility.Core.Messages
{
    public class PackageCompatibilityValidationMessageSerializer : IBrokeredMessageSerializer<PackageCompatibilityValidationMessage>
    {
        private const string PackageCompatibilityValidationSchema = "PackageCompatibilityValidationMessageData";

        private IBrokeredMessageSerializer<PackageCompatibilityValidationMessageData> _serializer =
            new BrokeredMessageSerializer<PackageCompatibilityValidationMessageData>();

        public IBrokeredMessage Serialize(PackageCompatibilityValidationMessage message)
        {
            return _serializer.Serialize(new PackageCompatibilityValidationMessageData
            {
                PackageId = message.PackageId,
                PackageVersion = message.PackageVersion,
                NupkgUri = message.NupkgUri,
                ValidationId = message.ValidationId
            });
        }

        public PackageCompatibilityValidationMessage Deserialize(IBrokeredMessage brokeredMessage)
        {
            var message = _serializer.Deserialize(brokeredMessage);

            return new PackageCompatibilityValidationMessage(
                message.PackageId,
                message.PackageVersion,
                message.NupkgUri,
                message.ValidationId);
        }

        [Schema(Name = PackageCompatibilityValidationSchema, Version = 1)]
        private struct PackageCompatibilityValidationMessageData
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public Uri NupkgUri { get; set; }
            public Guid ValidationId { get; set; }
        }
    }
}
