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
    /// <summary>
    /// Maintains a cache of configuration or command line arguments injected with secrets using an ISecretInjector and refreshes itself at a specified interval.
    /// </summary>
    public class RefreshingArgumentsDictionary : IArgumentsDictionary
    {
        public const string RefreshArgsIntervalSec = "CacheRefreshInterval";
        private const int DefaultRefreshIntervalSec = 60 * 60 * 24; // 1 day (24 hrs)

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

        /// <summary>
        /// Gets an argument from the cache.
        /// If the argument has not been cached or needs to be refreshed, it reinjects the KeyVault secrets and updates the cache.
        /// </summary>
        /// <param name="key">The key associated with the desired argument.</param>
        /// <returns>The argument associated with the given key.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the list of arguments.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the argument associated with the given key is null or empty.</exception>
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

        public async Task<T> GetOrThrow<T>(string key)
        {
            string argumentString = await Get(key);

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                // This will throw a NotSupportedException if no conversion is possible.
                return (T)converter.ConvertFromString(argumentString);
            }
            // If there is no converter, no conversion is possible, so throw a NotSupportedException.
            throw new NotSupportedException();
        }

        public async Task<T> GetOrDefault<T>(string key, T defaultValue = default(T))
        {
            try
            {
                return await GetOrThrow<T>(key);
            }
            catch (ArgumentNullException)
            {
                // The value for the specified key is null or empty.
            }
            catch (KeyNotFoundException)
            {
                // The specified key was not found in the arguments.
            }
            catch (NotSupportedException)
            {
                // Could not convert an object of type string into an object of type T.
            }
            return defaultValue;
        }

        public void Set(string key, string value)
        {
            _injectedArguments[key] = new Tuple<string, DateTime>(value, DateTime.UtcNow);
        }

        /// <summary>
        /// Fetches the argument from KeyVault and updates the cache.
        /// </summary>
        /// <param name="key">The key associated with the desired argument.</param>
        /// <returns>The argument, freshly updated from KeyVault.</returns>
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
