using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public interface IArgumentsDictionary
    {
        Task<T> Get<T>(string key);

        Task<T> GetOrDefault<T>(string key);

        void Set(string key, string value);

        bool ContainsKey(string key);

        bool Contains(KeyValuePair<string, string> item);

        void Add(KeyValuePair<string, string> item);

        void Add(string key, string value);

        bool Remove(KeyValuePair<string, string> item);

        bool Remove(string key);

        void ClearCache();
    }
}
