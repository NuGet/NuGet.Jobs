﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;

namespace Stats.RollUpDownloadFacts
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var job = new RollUpDownloadFactsJob();
            JobRunner.Run(job, args).Wait();
        }
    }
}
