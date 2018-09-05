using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator
{
    public interface IEntityCollectorProcessor
    {
        string Name { get; }

        Task<DateTime?> FetchSince(DateTime cursor);
    }
}
