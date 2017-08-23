using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public interface ILogDestination
    {
        bool TryWriteAsync(Stream stream, string destinationFileName);
    }
}
