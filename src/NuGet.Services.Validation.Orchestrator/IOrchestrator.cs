// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// A validation orchestrator interface
    /// </summary>
    public interface IOrchestrator
    {
        /// <summary>
        /// Returns the number of validation requests being handled at the moment of the call.
        /// Should be used to figure out when shutting down had finished.
        /// </summary>
        /// <returns>The number of validation requests being processed.</returns>
        Task<int> GetNumberOfRequestsInProgressAsync();

        /// <summary>
        /// Starts listening for incoming validation requests.
        /// </summary>
        Task StartProcessingMessagesAsync();
        
        /// <summary>
        /// Initiate shutdown sequence. Orchestrator will stop listening for new requests (requests being processed will continue running).
        /// </summary>
        /// <remarks>
        /// The calling code should call <see cref="GetNumberOfRequestsInProgressAsync"/> after calling this method repeatedly until it returns 0, 
        /// or the calling code runs out of patience.
        /// </remarks>
        Task StartShuttingDownAsync();
    }
}