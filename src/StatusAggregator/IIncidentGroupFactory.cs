using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IIncidentGroupFactory
    {
        Task<IncidentGroupEntity> CreateIncidentGroupAsync(IncidentEntity incidentEntity);
    }
}
