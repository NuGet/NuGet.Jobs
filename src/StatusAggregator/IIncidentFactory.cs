using System.Threading.Tasks;
using StatusAggregator.Incidents.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public interface IIncidentFactory
    {
        Task<IncidentEntity> CreateIncident(ParsedIncident parsedIncident);
    }
}
