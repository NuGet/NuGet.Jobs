using NuGet.Services.Status;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IStatusExporter
    {
        /// <summary>
        /// Builds a <see cref="ServiceStatus"/> and exports it to public storage so that it can be consumed by other services.
        /// </summary>
        Task Export();
    }
}
