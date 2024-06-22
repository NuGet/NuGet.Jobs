// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class UserCertificateValidator : IUserCertificateValidator
    {
        private readonly ILogger<UserCertificateValidator> _logger;

        public UserCertificateValidator(ILogger<UserCertificateValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Determines if the certificate is valid for signing the specified package registration.
        /// </summary>
        /// <param name="packageRegistration">A package registration.</param>
        /// <param name="certificate">A author signing certificate.</param>
        /// <returns>A flag indicating whether the certificate is acceptable for signing.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageRegistration" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="certificate" /> is <c>null</c>.</exception>
        public bool IsAcceptableSigningCertificate(PackageRegistration packageRegistration, X509Certificate2 certificate)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var requiredSigner = packageRegistration.RequiredSigners.FirstOrDefault();

            bool isAcceptable;
            if (requiredSigner != null)
            {
                isAcceptable = CanUseCertificate(requiredSigner, certificate);
            }
            else
            {
                isAcceptable = packageRegistration.Owners.Any(owner => CanUseCertificate(owner, certificate));
            }

            if (!isAcceptable)
            {
                _logger.LogWarning(
                    "Signed package blocked since it has an unknown certificate fingerprint: {UnknownFingerprint}",
                    certificate.ComputeSHA256Thumbprint());
            }

            return isAcceptable;
        }

        private bool CanUseCertificate(User user, X509Certificate2 certificate)
        {
            var thumbprint = certificate.ComputeSHA256Thumbprint();
            if (user.UserCertificates.Any(uc => string.Equals(uc.Certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
