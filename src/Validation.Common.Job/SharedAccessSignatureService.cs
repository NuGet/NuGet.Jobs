// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs.Validation
{
    public class SharedAccessSignatureService : ISharedAccessSignatureService
    {
        private readonly ISecretInjector _secretInjector;

        public SharedAccessSignatureService(ISecretInjector secretInjector)
        {
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public async Task<string> GetFromManagedStorageAccountAsync(string sasDefinition)
        {
            return await _secretInjector.InjectAsync($"$${sasDefinition}$$");
        }
    }
}
