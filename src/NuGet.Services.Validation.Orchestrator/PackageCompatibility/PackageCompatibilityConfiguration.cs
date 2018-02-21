// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.Orchestrator.PackageCompatibility
{
    /// <summary>
    /// Configuration for initializing the <see cref="PackageCompatibilityValidator"/>.
    /// </summary>
    public class PackageCompatibilityConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue package compatibility validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }
    }

}

