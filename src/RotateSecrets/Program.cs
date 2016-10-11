using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Jobs;

namespace RotateSecrets
{
    public class Program
    {
        public static void Main(string[] args)
        {
            JobRunner.Run(new Job(), args).Wait();
        }
    }
}
