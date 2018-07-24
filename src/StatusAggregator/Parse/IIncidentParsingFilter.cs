using NuGet.Services.Incidents;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public interface IIncidentParsingFilter
    {
        bool ShouldParse(Incident incident, GroupCollection groups);
    }
}
