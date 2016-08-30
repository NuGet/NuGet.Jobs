using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// A dictionary of configuration or command line arguments.
    /// </summary>
    public interface IArgumentsDictionary
    {
        /// <summary>
        /// Gets an argument from the dictionary.
        /// </summary>
        /// <typeparam name="T">Converts the argument from a string into this type.</typeparam>
        /// <param name="key">The key mapping to the desired argument.</param>
        /// <returns>The argument mapped to by the key converted to type T.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not mapped to an argument.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the argument mapped to by the key is null or empty.</exception>
        /// <exception cref="NotSupportedException">Thrown when the argument mapped to by the key cannot be converted into an object of type T.</exception>
        Task<T> GetOrThrow<T>(string key);

        /// <summary>
        /// Gets an argument from the dictionary.
        /// </summary>
        /// <typeparam name="T">Converts the argument from a string into this type.</typeparam>
        /// <param name="key">The key mapping to the desired argument.</param>
        /// <param name="defaultValue">The value returned if there is an issue getting the argument from the cache.</param>
        /// <returns>The argument mapped to by the key converted to type T or defaultValue if the argument could not be acquired and converted.</returns>
        Task<T> GetOrDefault<T>(string key, T defaultValue = default(T));

        void Set(string key, string value);

        bool ContainsKey(string key);

        bool Contains(KeyValuePair<string, string> item);

        void Add(KeyValuePair<string, string> item);

        void Add(string key, string value);

        bool Remove(KeyValuePair<string, string> item);

        bool Remove(string key);

        void Clear();
    }
}
