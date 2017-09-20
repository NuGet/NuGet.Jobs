// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation.Orchestrator
{
    public class OrchestrationRunnerConfiguration
    {
        public TimeSpan ProcessRecycleInterval { get; set; }
        public TimeSpan ShutdownWaitInterval { get; set; }
    }
}
