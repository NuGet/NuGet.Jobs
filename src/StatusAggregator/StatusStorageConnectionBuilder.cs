using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusStorageConnectionBuilder
    {
        public Func<StatusAggregatorConfiguration, string> GetConnectionString { get; }
        public string Name { get; }

        public StatusStorageConnectionBuilder(
            Func<StatusAggregatorConfiguration, string> getConnectionString,
            string name)
        {
            GetConnectionString = getConnectionString;
            Name = name;
        }
    }
}
