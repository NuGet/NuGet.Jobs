﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.PackageSigning
{
    /// <summary>
    /// Configuration for initializing the <see cref="PackageSigningValidator"/>.
    /// </summary>
    public class PackageSigningConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue package signing validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }
    }
}
