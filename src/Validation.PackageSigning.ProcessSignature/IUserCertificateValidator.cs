// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Entities;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface IUserCertificateValidator
    {
        bool IsAcceptableSigningCertificate(PackageRegistration packageRegistration, X509Certificate2 certificate);
    }
}