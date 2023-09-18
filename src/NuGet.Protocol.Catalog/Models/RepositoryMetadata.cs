// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class RepositoryMetadata
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("branch")]
        public string Branch { get; set; }

        [JsonProperty("commit")]
        public string Commit { get; set; }
    }
}