using Microsoft.WindowsAzure.Storage.Blob;
using StatusAggregator.Table;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusAggregator
    {
        private readonly CloudBlobContainer _container;
        private readonly ITableWrapper _table;

        private readonly IStatusUpdater _statusUpdater;
        private readonly IStatusExporter _statusExporter;

        public StatusAggregator(
            CloudBlobContainer container,
            ITableWrapper table,
            IStatusUpdater statusUpdater,
            IStatusExporter statusExporter)
        {
            _container = container;
            _table = table;
            _statusUpdater = statusUpdater;
            _statusExporter = statusExporter;
        }

        public async Task Run()
        {
            await _table.CreateIfNotExistsAsync();
            await _container.CreateIfNotExistsAsync();

            await _statusUpdater.Update();
            await _statusExporter.Export();
        }
    }
}
