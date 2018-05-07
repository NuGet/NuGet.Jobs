using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class PackageLagMonitorConfiguration
    {
        public int InstancePortMinimum { get; set; }

        public string ServiceIndexUrl { get; set; }

        public string ResourceGroup { get; set; }

        public string Subscription { get; set; }

        public string ServiceName { get; set; }

        public string Region { get; set; }
    }
}
