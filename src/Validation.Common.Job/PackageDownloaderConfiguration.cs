// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    public class PackageDownloaderConfiguration
    {
        /// <summary>
        /// The size of the buffer used to copy the network stream.
        /// </summary>
        /// <remarks>Implementation uses the <see cref="System.IO.Stream.CopyToAsync(System.IO.Stream, int, System.Threading.CancellationToken)"/>
        /// passing that value as the second argument.</remarks>
        public int BufferSize { get; set; }
    }
}
