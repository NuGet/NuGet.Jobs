using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public interface IManualStatusChangeUpdater
    {
        string Name { get; }

        Task<DateTime?> ProcessNewManualChanges(DateTime cursor);
    }
}
