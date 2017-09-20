// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Runs <see cref="IValidationMessageHandler"/> implementation set up in the DI container
    /// inside its personal scope.
    /// </summary>
    public class ScopedPackageValidationMessageHandler : IValidationMessageHandler
    {
        private readonly IServiceScopeProvider _serviceScopeProvider;

        private readonly object _validationMessageHandlerLock = new object();

        public ScopedPackageValidationMessageHandler(IServiceScopeProvider serviceScopeProvider)
        {
            _serviceScopeProvider = serviceScopeProvider ?? throw new ArgumentNullException(nameof(serviceScopeProvider));
        }

        public async Task OnMessageAsync(PackageValidationMessageData message)
        {
            using (var scope = _serviceScopeProvider.CreateScope())
            {
                IValidationMessageHandler messageHandler;
                // NuGet.Services.KeyVault.CachingSecretReader is not thread-safe and WILL be invoked
                // in IValidationMessageHandler construction process, we need lock around it to prevent
                // issues
                lock (_validationMessageHandlerLock)
                {
                    messageHandler = scope.ServiceProvider.GetRequiredService<IValidationMessageHandler>();
                }
                await messageHandler.OnMessageAsync(message);
            }
        }
    }
}
