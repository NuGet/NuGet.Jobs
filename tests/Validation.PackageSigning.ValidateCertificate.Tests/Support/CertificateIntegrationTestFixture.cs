// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Test.Utility.Signing;
using BCCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    using CoreCertificateIntegrationTestFixture = Core.Tests.Support.CertificateIntegrationTestFixture;

    public class CertificateIntegrationTestFixture : CoreCertificateIntegrationTestFixture
    {
        public async Task RevokeCertificateAuthority()
        {
            var ca = await GetCertificateAuthority();
            var rootCa = await GetRootCertificateAuthority();

            rootCa.Revoke(ca.Certificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);
        }

        public async Task<X509Certificate2> GetTimestampingCertificateAsync()
        {
            var ca = await GetCertificateAuthority();
            return CreateTimestampingCertificate(ca);
        }

        public X509Certificate2 CreateTimestampingCertificate(CertificateAuthority ca)
        {
            void CustomizeAsTimestampingCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddTimestampingEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Timestamping", CustomizeAsTimestampingCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetUnknownSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsUnknownSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: false, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Unknown Signing", CustomizeAsUnknownSigningCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedSigningCertificateAsync(DateTimeOffset revocationDate)
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Revoked Signing", CustomizeAsSigningCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<X509Certificate2> GetPartialChainSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsPartialChainSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: false);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Untrusted Signing", CustomizeAsPartialChainSigningCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetPartialChainAndRevokedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsPartialChainAndRevokedCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: false);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Untrusted and Revoked Signing", CustomizeAsPartialChainAndRevokedCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }

        public async Task<X509Certificate2> GetExpiredSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsExpiredCertificate(X509V3CertificateGenerator generator)
            {
                generator.MakeExpired();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetExpiredAndRevokedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsExpiredAndRevokedCertificate(X509V3CertificateGenerator generator)
            {
                generator.MakeExpired();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredAndRevokedCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }
    }
}
