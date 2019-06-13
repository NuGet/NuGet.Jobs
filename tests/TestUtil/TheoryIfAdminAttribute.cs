﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace TestUtil
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TheoryIfAdminAttribute : TheoryAttribute
    {
        public TheoryIfAdminAttribute()
        {
            if (!UserHelper.IsAdmin())
            {
                Skip = "Test will not run unless executed as Administrator";
            }
        }
    }
}
