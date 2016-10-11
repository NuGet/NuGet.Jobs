// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs
{
    public static class SecretReaderFactory
    {
        public static KeyVaultConfiguration CreateKeyVaultConfiguration(IDictionary<string, string> settings)
        {
            var vaultConfig = JobRunner.ServiceContainer.GetService<KeyVaultConfiguration>();
            if (vaultConfig != null) return vaultConfig;

            var storeName = JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.StoreName);
            var storeLocation = JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.StoreLocation);

            vaultConfig = new KeyVaultConfiguration(
                JobConfigurationManager.GetArgument(settings, JobArgumentNames.VaultName),
                JobConfigurationManager.GetArgument(settings, JobArgumentNames.ClientId),
                JobConfigurationManager.GetArgument(settings, JobArgumentNames.CertificateThumbprint),
                storeName != null ? (StoreName)Enum.Parse(typeof(StoreName), storeName) : StoreName.My,
                storeLocation != null ? (StoreLocation)Enum.Parse(typeof(StoreLocation), storeLocation) : StoreLocation.LocalMachine,
                JobConfigurationManager.TryGetBoolArgument(settings, JobArgumentNames.ValidateCertificate, defaultValue: true));

            JobRunner.ServiceContainer.AddService(vaultConfig);

            return vaultConfig;
        }

        public static ISecretReader CreateSecterReader(IDictionary<string, string> settings)
        {
            if (JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.VaultName) == null)
            {
                return new EmptySecretReader();
            }

            var vaultReader = JobRunner.ServiceContainer.GetService<KeyVaultReader>();
            if (vaultReader != null) return vaultReader;

            vaultReader = new KeyVaultReader(CreateKeyVaultConfiguration(settings));

            JobRunner.ServiceContainer.AddService(vaultReader);

            return vaultReader;
        }

        public static ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}