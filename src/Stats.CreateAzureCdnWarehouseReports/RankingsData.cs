// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class RankingsData
    {
        [JsonProperty("PackageId")]
        public string PackageId { get; set; }

        [JsonProperty("Downloads")]
        public long Downloads { get; set; }
    }
}