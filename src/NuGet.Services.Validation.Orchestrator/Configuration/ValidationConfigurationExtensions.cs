// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    internal static class ValidationConfigurationExtensions
    {
        private const string NuGetValidationStepContentType = "NuGet";

        public static List<ValidationConfigurationItem> GetClassicValidationConfiguration(this ValidationConfiguration validationConfiguration)
        {
            List<ValidationConfigurationItem> nugetList = null;
            if (validationConfiguration?.ValidationSteps?.TryGetValue(NuGetValidationStepContentType, out nugetList) == true)
            {
                return nugetList;
            }
            return null;
        }
    }
}
