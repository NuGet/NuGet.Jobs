// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IMessageChangeEventProcessor
    {
        /// <summary>
        /// Processes <paramref name="change"/>.
        /// </summary>
        /// <param name="cursor">The current timestamp.</param>
        /// <param name="eventEntity">The <see cref="EventEntity"/> associated with the change.</param>
        /// <param name="rootComponent">The <see cref="IComponent"/> associated with this iteration.</param>
        /// <param name="context">The <see cref="CurrentMessageContext"/> associated with this iteration.</param>
        /// <returns>A <see cref="CurrentMessageContext"/> that describes the change made.</returns>
        Task<CurrentMessageContext> Process(
            MessageChangeEvent change, 
            DateTime cursor, 
            EventEntity eventEntity, 
            IComponent rootComponent, 
            CurrentMessageContext context);
    }
}
