// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Revalidate
{
    public class CaseInsensitiveSet : HashSet<string>
    {
        public CaseInsensitiveSet()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public CaseInsensitiveSet(IEnumerable<string> collection)
            : base(collection, StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
