// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.PackageSigning
{
    public class ResourceNotFoundException : Exception
    {
        public Uri Uri { get; }

        public ResourceNotFoundException(Uri uri)
            : base($"Resource not found: {uri.AbsolutePath}")
        {
            Uri = uri;
        }
    }
}
