// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides instances of the <see cref="IServiceScope"/>
    /// </summary>
    /// <remarks>
    /// Needed to work around issues with mocking extension methods
    /// </remarks>
    public interface IServiceScopeProvider
    {
        /// <summary>
        /// Creates <see cref="IServiceScope"/>.
        /// </summary>
        /// <returns><see cref="IServiceScope"/> instance.</returns>
        IServiceScope CreateScope();
    }
}
