using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace Stats.CalculateTotals
{
    public class JsonLdHelper
    {
        public static JObject GetContext(string name, Uri type)
        {
            var jsonLdContext = new ConcurrentDictionary<string, JObject>();

            return jsonLdContext.GetOrAdd(name + "#" + type.ToString(), (key) =>
            {
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader(GetResourceStream(name))))
                {
                    JObject obj = JObject.Load(jsonReader);
                    obj["@type"] = type.ToString();
                    return obj;
                }
            });
        }

        private static Stream GetResourceStream(string resName)
        {
            string name = Assembly.GetExecutingAssembly().GetName().Name.Replace("-", ".");
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name + "." + resName);
        }
    }
}