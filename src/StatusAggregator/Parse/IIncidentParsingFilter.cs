using NuGet.Services.Incidents;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// An additional filter that can be applied to a <see cref="IncidentParser"/>
    /// </summary>
    public interface IIncidentParsingFilter
    {
        /// <summary>
        /// Returns whether or not an <see cref="IncidentParser"/> should parse <paramref name="incident"/>.
        /// </summary>
        bool ShouldParse(Incident incident, GroupCollection groups);
    }
}
