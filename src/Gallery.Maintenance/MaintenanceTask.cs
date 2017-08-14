using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gallery.Maintenance
{
    public abstract class MaintenanceTask : IMaintenanceTask
    {
        protected ILogger<IMaintenanceTask> _logger;

        public abstract Task RunAsync(Job job);

        public MaintenanceTask(ILogger<IMaintenanceTask> logger)
        {
            _logger = logger;
        }
    }
}
