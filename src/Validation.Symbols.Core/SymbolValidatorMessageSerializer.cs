// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolValidatorMessageSerializer : IBrokeredMessageSerializer<SymbolValidatorMessage>
    {
        private const string SchemaName = "SignatureValidationMessageData";

        private IBrokeredMessageSerializer<SymbolValidatorMessageData1> _serializer =
            new BrokeredMessageSerializer<SymbolValidatorMessageData1>();

        public SymbolValidatorMessage Deserialize(IBrokeredMessage message)
        {
            var deserializedMessage = _serializer.Deserialize(message);

            return new SymbolValidatorMessage(
                deserializedMessage.ValidationId,
                deserializedMessage.SymbolPackageKey,
                deserializedMessage.PackageId,
                deserializedMessage.PackageNormalizedVersion,
                deserializedMessage.SNupkgUrl);
        }

        public IBrokeredMessage Serialize(SymbolValidatorMessage message)
            => _serializer.Serialize(new SymbolValidatorMessageData1
            {
                ValidationId = message.ValidationId,
                SymbolPackageKey = message.SymbolPackageKey,
                PackageId = message.PackageId,
                PackageNormalizedVersion = message.PackageNormalizedVersion,
                SNupkgUrl = message.SNupkgUrl
            });

        [Schema(Name = SchemaName, Version = 1)]
        private class SymbolValidatorMessageData1
        {
            public Guid ValidationId { get; set; }

            public int SymbolPackageKey { get; set; }

            public string PackageId { get; set; }

            public string PackageNormalizedVersion { get; set; }

            public string SNupkgUrl { get; set; }
        }
    }
}
