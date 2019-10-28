// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    [Flags]
    public enum SigningCertificateUsage
    {
        None = 0,
        V1 = 1,
        V2 = 2
    }
}