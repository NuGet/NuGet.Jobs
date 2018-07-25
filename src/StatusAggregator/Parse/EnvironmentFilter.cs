using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Expects that the <see cref="Incident"/> contains a <see cref="Group"/> named <see cref="EnvironmentGroupName"/> with a whitelisted value.
    /// </summary>
    public class EnvironmentFilter : IIncidentParsingFilter
    {
        public const string EnvironmentGroupName = "Environment";

        private IEnumerable<string> _environments { get; }

        private readonly ILogger<EnvironmentFilter> _logger;

        public EnvironmentFilter(
            StatusAggregatorConfiguration configuration,
            ILogger<EnvironmentFilter> logger)
        {
            _environments = configuration.Environments;
            _logger = logger;
        }

        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            var group = groups[EnvironmentGroupName];

            if (group.Success)
            {
                var groupValue = group.Value;
                _logger.LogInformation("Incident has environment of {Environment}, expecting one of {Environments}.", 
                    groupValue, string.Join(";", _environments));
                return _environments.Any(
                    e => string.Equals(groups[EnvironmentGroupName].Value, e, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _logger.LogInformation("Incident does not have an enviroment group, will not filter by environment.");
                return true;
            }
        }
    }
}
