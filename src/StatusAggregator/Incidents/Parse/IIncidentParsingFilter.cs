using System.Text.RegularExpressions;

namespace StatusAggregator.Incidents.Parse
{
    public interface IIncidentParsingFilter
    {
        bool ShouldParse(Incident incident, GroupCollection groups);
    }
}
