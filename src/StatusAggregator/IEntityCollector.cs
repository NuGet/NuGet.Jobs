using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface IEntityCollector
    {
        string Name { get; }

        Task<DateTime> FetchLatest();
    }
}
