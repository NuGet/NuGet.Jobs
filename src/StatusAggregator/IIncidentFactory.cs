using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator
{
    /// <summary>
    /// Handles creation of <see cref="IncidentEntity"/>s.
    /// </summary>
    public interface IIncidentFactory
    {
        /// <summary>
        /// Creates a <see cref="IncidentEntity"/> from <paramref name="parsedIncident"/> and persists it in storage.
        /// </summary>
        Task<IncidentEntity> CreateIncident(ParsedIncident parsedIncident);
    }
}
