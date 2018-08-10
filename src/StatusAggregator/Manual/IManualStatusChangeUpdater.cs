using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public interface IManualStatusChangeUpdater
    {
        Task<DateTime?> ProcessNewManualChanges(DateTime cursor);
    }
}
