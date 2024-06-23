// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface IUserCertificateValidator
    {
        /// <summary>
        /// Determines if the author code signing certificate is valid for signing the specified package registration.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <param name="certificate">A author signing certificate.</param>
        /// <param name="extraStore">A certificate store containing additional certificates necessary for chain building.</param>
        /// <returns>Returns <c>null</c> if the certificate is valid or a descriptive <see cref="ValidationIssue"/> if the certificate is invalid.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificate" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="extraStore" /> is <c>null</c>.</exception>
        ValidationIssue ValidateCertificate(
            PackageRegistration packageRegistration,
            X509Certificate2 certificate,
            X509Certificate2Collection extraStore);
    }
}