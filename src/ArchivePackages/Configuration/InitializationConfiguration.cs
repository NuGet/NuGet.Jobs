// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ArchivePackages
{
    public class InitializationConfiguration
    {
        /// <summary>
        /// Source storage account.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Source storage container name.
        /// </summary>
        public string SourceContainerName { get; set; }

        /// <summary>
        /// Primary archive destination.
        /// </summary>
        public string PrimaryDestination { get; set; }

        /// <summary>
        /// Secondary archive destination.
        /// </summary>
        public string SecondaryDestination { get; set; }

        /// <summary>
        /// Source storage container name.
        /// </summary>
        public string DestinationContainerName { get; set; }

        /// <summary>
        /// Cursor blob name.
        /// </summary>
        public string CursorBlob { get; set; }

        /// <summary>
        /// Sleep interval between job run iterations.
        /// </summary>
        public int Sleep { get; set; }

        /// <summary>
        /// Application insights instrumentation key.
        /// </summary>
        public string InstrumentationKey { get; set; }
    }
}
