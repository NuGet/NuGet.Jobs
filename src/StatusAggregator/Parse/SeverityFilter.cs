using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Expects that the severity of an <see cref="Incident"/> must be lower than a threshold.
    /// </summary>
    public class SeverityFilter : IIncidentParsingFilter
    {
        private readonly int _maximumSeverity;

        private readonly ILogger<SeverityFilter> _logger;

        public SeverityFilter(
            StatusAggregatorConfiguration configuration,
            ILogger<SeverityFilter> logger)
        {
            _maximumSeverity = configuration.MaximumSeverity;
            _logger = logger;
        }
        
        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            var actualSeverity = incident.Severity;
            _logger.LogInformation(
                "Filtering incident severity: severity is {IncidentSeverity}, must be less than or equal to {MaximumSeverity}",
                actualSeverity, _maximumSeverity);
            return actualSeverity <= _maximumSeverity;
        }
    }
}
