// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.AzureManagement;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class AzureManagementAPIWrapperConfiguration : IAzureManagementAPIWrapperConfiguration
    {
        public AzureManagementAPIWrapperConfiguration(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        public string ClientId { get; }

        public string ClientSecret { get; }
    }
}
