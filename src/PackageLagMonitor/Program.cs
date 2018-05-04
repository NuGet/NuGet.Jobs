using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Jobs;

namespace NuGet.Jobs.PackageLagMonitor
{
    class Program
    {
        static int Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).Wait();
            return 0;
        }
    }
}
