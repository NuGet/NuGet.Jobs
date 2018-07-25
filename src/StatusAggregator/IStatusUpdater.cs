using NuGet.Services.Status;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IStatusUpdater
    {
        /// <summary>
        /// Aggregates the information necessary to build a <see cref="ServiceStatus"/> that describes the NuGet service.
        /// </summary>
        Task Update();
    }
}
