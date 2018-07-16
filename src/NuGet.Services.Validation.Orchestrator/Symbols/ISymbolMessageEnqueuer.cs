// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Symbols
{
    public interface ISymbolMessageEnqueuer
    {
        /// <summary>
        /// Enqueues a message to one of the topics used by the Symbol validators
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task EnqueueSymbolMessageAsync(IValidationRequest request);
    }
}
