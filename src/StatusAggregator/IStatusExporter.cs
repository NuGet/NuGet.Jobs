using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IStatusExporter
    {
        Task Export();
    }
}
