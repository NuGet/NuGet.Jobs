// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;

namespace StatusAggregator.Export
{
    public class StatusSerializer : IStatusSerializer
    {
        private const string StatusBlobName = "status.json";

        private readonly CloudBlobContainer _container;

        private readonly ILogger<StatusSerializer> _logger;

        private static readonly JsonSerializerSettings _statusBlobJsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new StatusContractResolver(),
            Converters = new List<JsonConverter>() { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        public StatusSerializer(
            CloudBlobContainer container,
            ILogger<StatusSerializer> logger)
        {
            _container = container;
            _logger = logger;
        }

        public async Task<ServiceStatus> Serialize(IComponent rootComponent, IEnumerable<Event> recentEvents)
        {
            ServiceStatus status;
            string statusJson;
            using (_logger.Scope("Serializing service status."))
            {
                status = new ServiceStatus(rootComponent, recentEvents);
                statusJson = JsonConvert.SerializeObject(status, _statusBlobJsonSerializerSettings);
            }

            using (_logger.Scope("Saving service status to blob storage."))
            {
                var blob = _container.GetBlockBlobReference(StatusBlobName);
                await blob.UploadTextAsync(statusJson);
            }

            return status;
        }
    }
}
