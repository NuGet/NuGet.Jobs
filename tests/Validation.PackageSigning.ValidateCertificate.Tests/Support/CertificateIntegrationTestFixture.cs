﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Validation.PackageSigning.Core.Tests.Support;
using BCCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    using CoreCertificateIntegrationTestFixture = Core.Tests.Support.CertificateIntegrationTestFixture;

    public class CertificateIntegrationTestFixture : CoreCertificateIntegrationTestFixture
    {
        public async Task<X509Certificate2> GetIntermediateCaCertificate()
        {
            var certificate = new X509Certificate2();
            var encodedCert = (await GetCertificateAuthority()).Certificate.GetEncoded();

            certificate.Import(encodedCert);

            return certificate;
        }

        public async Task RevokeCertificateAuthority()
        {
            var ca = await GetCertificateAuthority();
            var rootCa = await GetRootCertificateAuthority();

            rootCa.Revoke(
                ca.Certificate,
                reason: RevocationReason.Unspecified,
                revocationDate: DateTimeOffset.UtcNow);
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

            ca.Revoke(publicCertificate, reason: RevocationReason.Unspecified, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetRevokedSigningCertificateAsync(DateTimeOffset revocationDate, DateTimeOffset crlUpdateTime)
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthority();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            var ca2Responder = OcspResponder.Create(ca2, new OcspResponderOptions
            {
                ThisUpdate = crlUpdateTime,
            });

            responders.Add(testServer.RegisterResponder(ca2));
            responders.Add(testServer.RegisterResponder(ca2Responder));

            void CustomizeAsSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Revoked Signing", CustomizeAsSigningCertificate);

            var caCert = new X509Certificate2();
            var ca2Cert = new X509Certificate2();

            caCert.Import(ca.Certificate.GetEncoded());
            ca2Cert.Import(ca2.Certificate.GetEncoded());

            ca2.Revoke(publicCertificate, reason: RevocationReason.Unspecified, revocationDate: revocationDate);

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        new[] { caCert, ca2Cert },
                        responders);
        }

        public async Task<X509Certificate2> GetRevokedTimestampingCertificateAsync(DateTimeOffset revocationDate)
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddTimestampingEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Revoked Timestamping", CustomizeAsSigningCertificate);

            ca.Revoke(publicCertificate, reason: RevocationReason.Unspecified, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthority();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            var responders = GetResponders();

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            rootCa.Revoke(intermediateCa.Certificate, reason: RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return CreateSigningCertificate(intermediateCa);
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetPartialChainSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthority();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            responders.Add(testServer.RegisterResponder(ca2.OcspResponder));

            void CustomizeAsPartialChainSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Untrusted Signing", CustomizeAsPartialChainSigningCertificate);

            var caCert = new X509Certificate2();
            var ca2Cert = new X509Certificate2();

            caCert.Import(ca.Certificate.GetEncoded());
            ca2Cert.Import(ca2.Certificate.GetEncoded());

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        new[] { caCert, ca2Cert },
                        responders);
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetPartialChainAndRevokedSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthority();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            responders.Add(testServer.RegisterResponder(ca2.OcspResponder));

            void CustomizeAsPartialChainAndRevokedCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Untrusted and Revoked Signing", CustomizeAsPartialChainAndRevokedCertificate);

            ca2.Revoke(publicCertificate, reason: RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        new X509Certificate2[0],
                        responders);
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

            ca.Revoke(publicCertificate, reason: RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }

        public async Task<X509Certificate2> GetWeakSignatureParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthority();

            var issueOptions = IssueCertificateOptions.CreateDefaultForIntermediateCertificateAuthority();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 512);

            issueOptions.KeyPair = keyPair;

            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority(issueOptions);
            var responders = GetResponders();

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return CreateSigningCertificate(intermediateCa);
        }

        public class CertificateWithCustomIntermediatesResult : IDisposable
        {
            private IDisposable _responders;

            public CertificateWithCustomIntermediatesResult(
                X509Certificate2 endCertificate,
                X509Certificate2[] intermediateCertificates,
                IDisposable responders)
            {
                EndCertificate = endCertificate;
                IntermediateCertificates = intermediateCertificates;
                _responders = responders;
            }

            public X509Certificate2 EndCertificate { get; }
            public X509Certificate2[] IntermediateCertificates { get; }

            public void Dispose() => _responders.Dispose();
        }
    }
}
