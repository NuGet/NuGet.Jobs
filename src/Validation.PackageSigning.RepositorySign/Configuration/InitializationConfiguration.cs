// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Validation.PackageSigning.RepositorySign
{
    public class InitializationConfiguration
    {
        /// <summary>
        /// The list of filesystem paths that contain packages that are preinstalled by Visual Studio and .NET.
        /// Environment variables contained in the path will be expanded before evaluation.
        /// </summary>
        public List<string> PreinstalledPaths { get; set; }

        /// <summary>
        /// The revalidation job should not revalidate packages that were uploaded after repository signing was
        /// enabled. This value should be the key after which all new Package entities were repository signed.
        /// This upper bound key is exclusive and will not be included in the revalidation.
        /// </summary>
        public int MaxPackageKey { get; set; }

        /// <summary>
        /// The time to sleep between initialization batches to prevent overloading databases.
        /// </summary>
        public TimeSpan SleepDurationBetweenBatches { get; set; }
    }
}
