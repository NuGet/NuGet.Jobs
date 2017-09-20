﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Configuration source for the <see cref="KeyVaultInjectingConfigurationProvider"/> that wraps it around <see cref="JsonConfigurationProvider"/>
    /// to inject secrets to data read from json configuration
    /// </summary>
    public class KeyVaultJsonInjectingConfigurationSource : IConfigurationSource
    {
        private readonly string _path;
        private readonly ISecretInjector _secretInjector;

        public KeyVaultJsonInjectingConfigurationSource(string path, ISecretInjector secretInjector)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var jsonSource = new JsonConfigurationSource { FileProvider = null, Path = _path, Optional = false, ReloadOnChange = false };
            jsonSource.ResolveFileProvider();
            var jsonProvider = jsonSource.Build(builder);

            return new KeyVaultInjectingConfigurationProvider(jsonProvider, _secretInjector);
        }
    }
}
