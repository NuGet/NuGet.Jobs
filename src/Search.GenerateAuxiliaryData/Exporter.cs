// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Search.GenerateAuxiliaryData
{
    // Public only to facilitate testing.
    public abstract class Exporter
    {
        protected ILogger<Exporter> _logger;

        public CloudBlobContainer DestinationContainer { get; }
        public string Name { get; }

        public Exporter(ILogger<Exporter> logger, CloudBlobContainer defaultDestinationContainer, string defaultName)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            DestinationContainer = defaultDestinationContainer ?? throw new ArgumentNullException(nameof(defaultDestinationContainer));

            Name = defaultName;
        }

        public abstract Task ExportAsync();
    }
}
