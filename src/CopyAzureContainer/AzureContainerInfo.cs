// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace CopyAzureContainer
{
    public class AzureContainerInfo
    {
        public string StorageAccountName
        {
            private set;
            get;
        }

        public string StorageAccountKey
        {
            private set;
            get;
        }

        public string ContainerName
        {
            private set;
            get;
        }

        public AzureContainerInfo(string argument)
        {
            string[] info = argument.Split(':');
            StorageAccountName = info[0];
            StorageAccountKey = info[1];
            ContainerName = info[2];
        }
    }
}
