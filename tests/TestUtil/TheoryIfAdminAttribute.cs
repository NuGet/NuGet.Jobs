﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using Xunit.Sdk;

namespace TestUtil
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Xunit.Sdk.TheoryDiscoverer", "xunit.execution.{Platform}")]
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
