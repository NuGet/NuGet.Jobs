using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public interface ILogSource
    {
        /// <summary>
        /// It returns the files' uri existent at the source.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Uri> GetFiles();

        Task<bool> TryDeleteAsync(Uri file);

        Task<Stream> OpenReadAsync(Uri uri);

        bool TryLock(Uri file);
    }
}
