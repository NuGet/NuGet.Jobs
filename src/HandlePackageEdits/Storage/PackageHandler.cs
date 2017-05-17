using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandlePackageEdits.Storage
{
    public abstract class PackageHandler
    {
        public abstract string Name { get; }

        public abstract Uri Uri { get; }
    }
}
