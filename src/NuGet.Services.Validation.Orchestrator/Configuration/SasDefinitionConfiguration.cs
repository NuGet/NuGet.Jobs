// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    public class SasDefinitionConfiguration
    {
        public string PackageStatusProcessorSasDefinition { get; set; }
        public string ValidationSetProviderSasDefinition { get; set; }
        public string ValidationSetProcessorSasDefinition { get; set; }
    }
}
