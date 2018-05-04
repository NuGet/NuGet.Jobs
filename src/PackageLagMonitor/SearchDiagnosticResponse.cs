using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.PackageLagMonitor
{
    public class SearchDiagnosticResponse
    {
        public long NumDocs { get; set; }
        public string IndexName { get; set; }
        public long LastIndexReloadDurationInMilliseconds { get; set; }
        public DateTimeOffset LastIndexReloadTime { get; set; }
        public DateTimeOffset LastReopen { get; set; }
        public CommitUserData CommitUserData { get; set; }
    }

    public class CommitUserData
    {
        public string CommitTimeStamp { get; set; }
        public string Description { get; set; }
        public string Count { get; set; }
        public string Trace { get; set; }
    }
}
