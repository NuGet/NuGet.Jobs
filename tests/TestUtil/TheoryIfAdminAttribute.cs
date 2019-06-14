﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using Xunit.Sdk;

namespace TestUtil
{
    /// <summary>
    /// Indicates a test set that can be set up to be skipped when run as non-Admin user if environment
    /// is set up appropriately. See <see cref="UserHelper.EnableSkipVariableName"/> for more details.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Xunit.Sdk.TheoryDiscoverer", "xunit.execution.{Platform}")]
    public class TheoryIfAdminAttribute : TheoryAttribute
    {
        public TheoryIfAdminAttribute()
        {
            UserHelper.SetupFactSkipIfAdmin(this);
        }
    }
}
