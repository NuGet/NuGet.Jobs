using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class Constants
    {
        public static class BlobMetadata
        {
            public const string CacheControlProperty = "CacheControl";
            public const string CacheControlDefaultValue = "max-age=120";
        }
    }
}
