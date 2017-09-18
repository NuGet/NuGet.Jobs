using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Stores configuration for the "Validate only" run mode
    /// </summary>
    public class ValidateOnlyConfiguration
    {
        /// <summary>
        /// Indicates whether the orchestrator should only check the configuration (true) or run the service (false)
        /// </summary>
        public bool ValidateOnly { get; set; }
    }
}
