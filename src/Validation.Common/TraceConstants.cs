// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    public static class TraceConstants
    {
        public const string EventName = "EventName";
        public const string ValidatorName = "ValidatorName";
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";

        public const string PackageUnclean = "PackageUnclean";
        public const string RequestNotFound = "RequestNotFound";
        public const string InvestigationNeeded = "InvestigationNeeded";
    }
}
