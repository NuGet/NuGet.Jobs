// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Certificates;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class UserCertificateValidator : IUserCertificateValidator
    {
        /// <summary>
        /// This OID is used to mark all Trusted Signing Public Trust certificates.
        /// Source: https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-cert-management
        /// </summary>
        private const string AzureTrustedSigningPublicTrustEku = "1.3.6.1.4.1.311.97.1.0";

        /// <summary>
        /// This OID prefix is specific to the user.
        /// Source: https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-cert-management
        /// </summary>
        private const string AzureTrustedSigningPublicTrustIdentifierPrefix = "1.3.6.1.4.1.311.97.";

        private readonly ILogger<UserCertificateValidator> _logger;

        public UserCertificateValidator(ILogger<UserCertificateValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ValidationIssue ValidateCertificate(PackageRegistration packageRegistration, X509Certificate2 certificate, X509Certificate2Collection extraStore)
        {
            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (extraStore == null)
            {
                throw new ArgumentNullException(nameof(extraStore));
            }

            // SHA-1 usage is generally prohibited but this is only used for logging and in an issue property unused for display purposes.
            var sha1Thumbprint = certificate.Thumbprint.ToLowerInvariant();
            var sha256Thumbprint = certificate.ComputeSHA256Thumbprint();

            ICollection<User> usersToCheck;
            var requiredSigner =  packageRegistration.RequiredSigners.FirstOrDefault();
            if (requiredSigner != null)
            {
                usersToCheck = new[] { requiredSigner };
                _logger.LogInformation(
                    "Validating author signing certificate with SHA-256 fingerprint {Fingerprint} and SHA-1 fingerprint {Sha1Fingerprint}. A required signer is set on package ID {PackageId}.",
                    sha256Thumbprint,
                    sha1Thumbprint,
                    packageRegistration.Id);
            }
            else
            {
                usersToCheck = packageRegistration.Owners;
                _logger.LogInformation(
                    "Validating author signing certificate with SHA-256 fingerprint {Fingerprint} and SHA-1 fingerprint {Sha1Fingerprint}. No required signer is set on package ID {PackageId}. There are {Count} owners.",
                    sha256Thumbprint,
                    sha1Thumbprint,
                    packageRegistration.Id,
                    usersToCheck.Count);
            }

            var fingerprintChecks = 0;
            var patternChecks = 0;

            var remainingTypes = usersToCheck.SelectMany(u => u.UserCertificatePatterns).Select(p => p.PatternType).ToHashSet();
            var typeToPatterns = usersToCheck.SelectMany(u => u.UserCertificatePatterns).ToLookup(p => p.PatternType);
            var isAzureTrustedSigning = IsAzureTrustedSigning(certificate, extraStore, out var azureTrustedSigningOid);

            // check for Azure Trusted signing
            if (isAzureTrustedSigning)
            {
                _logger.LogInformation(
                    "Azure Trusted Signing certificate found with fingerprint {Fingerprint} and EKU {Oid}.",
                    sha256Thumbprint,
                    azureTrustedSigningOid);

                remainingTypes.Remove(CertificatePatternType.AzureTrustedSigning);

                foreach (var pattern in typeToPatterns[CertificatePatternType.AzureTrustedSigning])
                {
                    patternChecks++;
                    if (pattern.Identifier == azureTrustedSigningOid)
                    {
                        return null;
                    }
                }
            }

            // abort the flow if there are unrecognized pattern types
            if (remainingTypes.Count > 0)
            {
                throw new NotImplementedException($"Some pattern types are not implemented: {string.Join(", ", remainingTypes)}.");
            }

            // check for matching fingerprints
            foreach (var user in usersToCheck)
            {
                foreach (var userCertificate in user.UserCertificates)
                {
                    fingerprintChecks++;
                    if (string.Equals(userCertificate.Certificate.Thumbprint, sha256Thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
            }

            _logger.LogWarning(
                "Certificate with fingerprint {Fingerprint} does not match any allow fingerprint or certificate pattern. Pattern checks: {PatternChecks}. Fingerprint checks: {FingerprintChecks}.",
                sha256Thumbprint,
                patternChecks,
                fingerprintChecks);

            if (isAzureTrustedSigning)
            {
                return new UnauthorizedAzureTrustedSigningCertificateFailure(sha1Thumbprint, sha256Thumbprint, azureTrustedSigningOid);
            }
            else
            {

                return new UnauthorizedCertificateSha256Failure(sha1Thumbprint, sha256Thumbprint);
            }
        }

        private bool IsAzureTrustedSigning(X509Certificate2 certificate, X509Certificate2Collection extraStore, out string enhancedKeyUsageOid)
        {
            enhancedKeyUsageOid = null;

            // Azure Trusted Signing certificates have a specific root.
            // Source: https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-trust-models
            if (!IsRootCertificateMatching(certificate, extraStore, PinnedCertificates.MicrosoftIdentityVerificationRootCertificateAuthority2020))
            {
                return false;
            }

            var hasPublicTrustEku = false;
            foreach (var eku in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            {
                foreach (var oid in eku.EnhancedKeyUsages)
                {
                    if (oid.Value != AzureTrustedSigningPublicTrustEku && oid.Value.StartsWith(AzureTrustedSigningPublicTrustIdentifierPrefix))
                    {
                        enhancedKeyUsageOid = oid.Value;
                    }

                    if (oid.Value == AzureTrustedSigningPublicTrustEku)
                    {
                        hasPublicTrustEku = true;
                    }
                }
            }

            return hasPublicTrustEku && enhancedKeyUsageOid != null;
        }

        private bool IsRootCertificateMatching(X509Certificate2 leaf, X509Certificate2Collection extraStore, X509Certificate2 root)
        {
            try
            {
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.ExtraStore.Add(root);
                    chain.ChainPolicy.ExtraStore.AddRange(extraStore);

                    // More strict validation will occur elsewhere. The goal here is to simply get a chain with a well defined root.
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    if (!chain.Build(leaf))
                    {
                        var flattenedChainStatus = chain.ChainStatus.Aggregate(
                            chain.ChainStatus.Length == 0 ? X509ChainStatusFlags.PartialChain : X509ChainStatusFlags.NoError,
                            (agg, item) => agg | item.Status);
                        _logger.LogWarning(
                            "Failed to build certificate chain for certificate with fingerprint {Fingerprint}. Chain status: {ChainStatus}.",
                            leaf.ComputeSHA256Thumbprint(),
                            flattenedChainStatus);
                        return false;
                    }

                    var actualRootFingerprint = chain.ChainElements[chain.ChainElements.Count - 1].Certificate.ComputeSHA256Thumbprint();
                    var expectedRootFingerprint = root.ComputeSHA256Thumbprint();

                    _logger.LogInformation(
                        "For leaf certificate {Fingerprint}, comparing actual root certificate {ActualRootFingerprint} against root certificate {ExpectedRootFingerprint}.",
                        leaf.ComputeSHA256Thumbprint(),
                        actualRootFingerprint,
                        expectedRootFingerprint);

                    return expectedRootFingerprint == actualRootFingerprint;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not verify the root certificate. Failed to build certificate chain for certificate with fingerprint {Fingerprint}.",
                    leaf.ComputeSHA256Thumbprint());
                return false;
            }
        }
    }
}
