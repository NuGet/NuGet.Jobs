using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public interface IHttpResponseMessageWrapper : IDisposable
    {
        bool IsSuccessStatusCode { get; }

        string ReasonPhrase { get; set; }

        HttpStatusCode StatusCode { get; set; }

        IHttpContentWrapper Content { get; }
    }
}
