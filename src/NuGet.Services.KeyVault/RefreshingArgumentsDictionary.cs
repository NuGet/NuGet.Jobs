using NuGet.Services.KeyVault;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    public class RefreshingArgumentsDictionary : IArgumentsDictionary
    {
        public const string RefreshArgsIntervalSec = "RefreshArgsIntervalSec";
        private const int DefaultRefreshIntervalSec = 86400; // 1 day (24 hrs)

        private int _refreshArgsIntervalSec;
        private ISecretInjector _secretInjector;

        private Dictionary<string, string> _unprocessedArguments;
        private Dictionary<string, Tuple<string, DateTime>> _injectedArguments;

        public RefreshingArgumentsDictionary(ISecretInjector secretInjector, Dictionary<string, string> unprocessedArguments)
        {
            _secretInjector = secretInjector;
            _unprocessedArguments = unprocessedArguments;
            _injectedArguments = new Dictionary<string, Tuple<string, DateTime>>();

            var refreshArgsIntervalSec = DefaultRefreshIntervalSec;
            if (_unprocessedArguments.ContainsKey(RefreshArgsIntervalSec))
            {
                int parsedRefreshInterval;
                if (int.TryParse(_unprocessedArguments[RefreshArgsIntervalSec], out parsedRefreshInterval)) refreshArgsIntervalSec = parsedRefreshInterval;
            }
            _refreshArgsIntervalSec = refreshArgsIntervalSec;
        }

        private async Task<string> Get(string key)
        {
            if (!_unprocessedArguments.ContainsKey(key)) throw new KeyNotFoundException();

            if (!_injectedArguments.ContainsKey(key) || DateTime.UtcNow.Subtract(_injectedArguments[key].Item2).TotalSeconds >= _refreshArgsIntervalSec)
            {
                await processArgument(key);
            }

            var argumentValue = _injectedArguments[key].Item1;
            if (string.IsNullOrEmpty(argumentValue)) throw new ArgumentNullException();

            return argumentValue;
        }

        public async Task<T> Get<T>(string key)
        {
            string argumentString = await Get(key);
            var converter = TypeDescriptor.GetConverter(typeof(T));

            if (converter != null && !string.IsNullOrEmpty(argumentString)) return (T)converter.ConvertFromString(argumentString);
            return default(T);
        }

        public async Task<T> GetOrDefault<T>(string key)
        {
            try
            {
                return await Get<T>(key);
            }
            catch (ArgumentNullException)
            {
                // in arguments but null
            }
            catch (KeyNotFoundException)
            {
                // not in arguments
            }
            catch (NotSupportedException)
            {
                // could not convert
            }
            return default(T);
        }

        public void Set(string key, string value)
        {
            _injectedArguments[key] = new Tuple<string, DateTime>(value, DateTime.UtcNow);
        }

        private async Task<string> processArgument(string key)
        {
            var processedArgument = await _secretInjector.InjectAsync(_unprocessedArguments[key]);
            Set(key, processedArgument);
            return processedArgument;
        }

        public bool ContainsKey(string key)
        {
            return _unprocessedArguments.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return ContainsKey(item.Key) && Get(item.Key).Equals(item.Value);
        }

        public void Add(KeyValuePair<string, string> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(string key, string value)
        {
            _unprocessedArguments.Add(key, value);
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return Contains(item) && Remove(item.Key);
        }

        public bool Remove(string key)
        {
            return _unprocessedArguments.Remove(key) && _injectedArguments.Remove(key);
        }

        public void Clear()
        {
            _unprocessedArguments.Clear();
            _injectedArguments.Clear();
        }

        public void ClearCache()
        {
            _injectedArguments.Clear();
        }
    }
}
