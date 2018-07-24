using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface ICursor
    {
        Task<DateTime> Get();
        Task Set(DateTime value);
    }
}
