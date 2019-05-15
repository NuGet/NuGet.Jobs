using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public interface IHttpContentWrapper : IDisposable
    {
        Task<string> ReadAsStringAsync();
    }
}
