// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Validation.Orchestrator
{
    internal static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddInjectedJsonFile(this IConfigurationBuilder configurationBuilder, string path, ISecretInjector secretInjector)
        {
            configurationBuilder.Add(new KeyVaultJsonInjectingConfigurationSource(path, secretInjector));

            return configurationBuilder;
        }
    }
}
