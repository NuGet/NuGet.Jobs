// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Extensions.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace Microsoft.Services.Search.Web
{
    public class StartupHelper
    {
        public const string EnvironmentVariablePrefix = "APPSETTING_";

        public static RefreshableConfiguration GetSecretInjectedConfiguration(IConfigurationRoot uninjectedConfiguration)
        {
            // Initialize KeyVault integration.
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var refreshSecretReaderSettings = new RefreshableSecretReaderSettings();
            var refreshingSecretReaderFactory = new RefreshableSecretReaderFactory(secretReaderFactory, refreshSecretReaderSettings);
            var secretReader = refreshingSecretReaderFactory.CreateSecretReader();
            var secretInjector = refreshingSecretReaderFactory.CreateSecretInjector(secretReader);

            // Attempt to inject secrets into all of the configuration strings.
            foreach (var pair in uninjectedConfiguration.AsEnumerable())
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    // We can synchronously wait here because we are outside of the request context. It's not great
                    // but we need to fetch the initial secrets for the cache before activating any controllers or
                    // asking DI for configuration.
                    secretInjector.InjectAsync(pair.Value).Wait();
                }
            }

            // Reload the configuration with secret injection enabled. This is used by the application.
            var injectedBuilder = new ConfigurationBuilder()
                .AddInjectedJsonFile("appsettings.json", secretInjector)
                .AddInjectedJsonFile("appsettings.Development.json", secretInjector)
                .AddInjectedEnvironmentVariables(EnvironmentVariablePrefix, secretInjector);
            var injectedConfiguration = injectedBuilder.Build();

            // Now disable all secrets loads from a non-refresh path. Refresh will be called periodically from a
            // background thread. Foreground (request) threads MUST use the cache otherwise there will be a deadlock.
            refreshSecretReaderSettings.BlockUncachedReads = true;

            return new RefreshableConfiguration
            {
                SecretReaderFactory = refreshingSecretReaderFactory,
                Root = injectedConfiguration,
            };
        }
    }

    public class RefreshableConfiguration
    {
        public IRefreshableSecretReaderFactory SecretReaderFactory { get; set; }
        public IConfigurationRoot Root { get; set; }
    }
}
