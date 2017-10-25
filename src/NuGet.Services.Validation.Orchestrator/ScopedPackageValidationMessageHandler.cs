// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Runs <see cref="IValidationMessageHandler"/> implementation set up in the DI container
    /// inside its personal scope.
    /// </summary>
    public class ScopedPackageValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly IServiceScopeProvider _serviceScopeProvider;

        private readonly object _validationMessageHandlerLock = new object();

        public ScopedPackageValidationMessageHandler(IServiceScopeProvider serviceScopeProvider)
        {
            _serviceScopeProvider = serviceScopeProvider ?? throw new ArgumentNullException(nameof(serviceScopeProvider));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            using (var scope = _serviceScopeProvider.CreateScope())
            {
                IMessageHandler<PackageValidationMessageData> messageHandler;
                // NuGet.Services.KeyVault.CachingSecretReader is not thread-safe and WILL be invoked
                // in IValidationMessageHandler construction process, we need lock around it to prevent
                // issues
                lock (_validationMessageHandlerLock)
                {
                    messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler<PackageValidationMessageData>>();
                }
                return await messageHandler.HandleAsync(message);
            }
        }
    }
}
