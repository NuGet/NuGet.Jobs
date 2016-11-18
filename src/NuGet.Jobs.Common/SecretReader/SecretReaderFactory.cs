// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public ISecretReader CreateSecretReader(IDictionary<string, string> settings)
        {
            var vaultName = settings.GetOrDefault<string>(JobArgumentNames.VaultName);
            if (vaultName == null)
            {
                return new EmptySecretReader();
            }

            var keyVaultConfiguration =
                new KeyVaultConfiguration(
                    vaultName,
                    settings[JobArgumentNames.ClientId],
                    settings[JobArgumentNames.CertificateThumbprint],
                    settings.GetOrDefault(JobArgumentNames.StoreName, StoreName.My),
                    settings.GetOrDefault(JobArgumentNames.StoreLocation, StoreLocation.LocalMachine),
                    settings.GetOrDefault(JobArgumentNames.ValidateCertificate, true));

            var refreshIntervalSec = settings.GetOrDefault<int>(JobArgumentNames.RefreshIntervalSec,
                CachingSecretReader.DefaultRefreshIntervalSec);

            return new CachingSecretReader(new KeyVaultReader(keyVaultConfiguration), refreshIntervalSec);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}