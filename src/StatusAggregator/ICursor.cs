using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    /// <summary>
    /// Maintains the current progress of the job.
    /// </summary>
    public interface ICursor
    {
        Task<DateTime> Get();
        Task Set(DateTime value);
    }
}
