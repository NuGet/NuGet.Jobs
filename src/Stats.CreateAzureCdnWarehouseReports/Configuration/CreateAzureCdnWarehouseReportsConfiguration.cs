// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class CreateAzureCdnWarehouseReportsConfiguration
    {
        public string AzureCdnCloudStorageAccount { get; set; }

        public string AzureCdnCloudStorageContainerName { get; set; }

        public string DataStorageAcount { get; set; }

        public string DataContainerName { get; set; }

        public int? CommandTimeOut { get; set; }

        public int? PerPackageReportDegreeOfParallelism { get; set; }

        public string ReportName { get; set; }
    }
}
