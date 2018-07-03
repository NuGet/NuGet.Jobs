using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IStatusUpdater
    {
        Task Update();
    }
}
