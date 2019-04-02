// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Gallery.Maintenance
{
    public class ExpiredDeletedAccount
    {
        public int Key { get; set; }

        public string Username { get; set; }

        public DateTime DeletedOn { get; set; }
    }
}
