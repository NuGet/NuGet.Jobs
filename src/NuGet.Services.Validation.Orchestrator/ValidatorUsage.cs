// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Denotes how validator is used
    /// </summary>
    public enum ValidatorUsage
    {
        /// <summary>
        /// Indicates that validator must succeed in order to proceed
        /// </summary>
        Required,

        /// <summary>
        /// Indicates that validator must not be started (but configuration may be kept).
        /// Validators that are configured to be disabled, but that are running (they
        /// may have started before configuration update) are treated the same as in 
        /// <see cref="Required"/> status.
        /// </summary>
        Disabled,
    }
}
