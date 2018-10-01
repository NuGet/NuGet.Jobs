// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;

namespace StatusAggregator
{
    public class StatusExporter : IStatusExporter
    {
        private readonly ILogger<StatusExporter> _logger;

        public StatusExporter(ILogger<StatusExporter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServiceStatus> Export()
        {
            using (_logger.Scope("Exporting service status."))
            {
                _logger.LogInformation("TODO");

                return null;
            }
        }
    }
}
