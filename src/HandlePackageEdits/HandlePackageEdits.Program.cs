﻿using NuGet.Jobs;

namespace HandlePackageEdits
{
    class Program
    {
        static void Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).Wait();
        }
    }
}
