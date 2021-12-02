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
            if (validationConfiguration?.ValidationSteps?.TryGetValue(NuGetValidationStepContentType, out var nugetList) == true)
            {
                return nugetList;
            }
            return null;
        }
    }
}
