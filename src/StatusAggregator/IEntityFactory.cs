using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator
{
    public interface IEntityFactory<TEntity>
        where TEntity : TableEntity
    {
        Task<TEntity> Create(ParsedIncident input);
    }
}
