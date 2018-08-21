﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;
using StatusAggregator.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusAggregator
    {
        private readonly IEnumerable<CloudBlobContainer> _containers;
        private readonly IEnumerable<ITableWrapper> _tables;

        private readonly IStatusUpdater _statusUpdater;
        private readonly IStatusExporter _statusExporter;

        public StatusAggregator(
            IEnumerable<CloudBlobContainer> containers,
            IEnumerable<ITableWrapper> tables,
            IStatusUpdater statusUpdater,
            IStatusExporter statusExporter)
        {
            _containers = containers ?? throw new ArgumentNullException(nameof(containers));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _statusUpdater = statusUpdater ?? throw new ArgumentNullException(nameof(statusUpdater));
            _statusExporter = statusExporter ?? throw new ArgumentNullException(nameof(statusExporter));
        }

        public async Task Run()
        {
            // Initialize all tables and containers.
            await Task.WhenAll(_tables.Select(t => t.CreateIfNotExistsAsync()));
            await Task.WhenAll(_containers.Select(c => c.CreateIfNotExistsAsync()));
            
            // Update and export the status.
            await _statusUpdater.Update();
            await _statusExporter.Export();
        }
    }
}
