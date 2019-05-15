using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class HttpResponseMessageWrapper : IHttpResponseMessageWrapper
    {
        private HttpResponseMessage _responseMessage;

        public HttpResponseMessageWrapper(HttpResponseMessage message)
        {
            _responseMessage = message;
        }

        public IHttpContentWrapper Content {
            get
            {
                return new HttpContentWrapper(_responseMessage.Content);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _responseMessage.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
