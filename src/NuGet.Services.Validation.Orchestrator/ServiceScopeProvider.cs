// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Wrapper around <see cref="ServiceProviderServiceExtensions.CreateScope(IServiceProvider)"/> to enable
    /// mocking of the extension method that otherwise cannot be mocked.
    /// </summary>
    public class ServiceScopeProvider : IServiceScopeProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceScopeProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IServiceScope CreateScope()
        {
            return _serviceProvider.CreateScope();
        }
    }
}
