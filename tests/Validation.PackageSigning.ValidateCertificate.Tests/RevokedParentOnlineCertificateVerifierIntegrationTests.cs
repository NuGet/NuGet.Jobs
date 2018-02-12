// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using Validation.PackageSigning.ValidateCertificate.Tests.Support;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    using CertificateIntegrationTestFixure = Support.CertificateIntegrationTestFixture;

    [Collection(RevokedParentCertificateIntegrationTestCollection.Name)]
    public class RevokedParentOnlineCertificateVerifierIntegrationTests
    {
        private readonly CertificateIntegrationTestFixure _fixture;
        private readonly OnlineCertificateVerifier _target;

        public RevokedParentOnlineCertificateVerifierIntegrationTests(CertificateIntegrationTestFixure fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

            _target = new OnlineCertificateVerifier();
        }

        [Fact]
        public async Task RevokedParentCertificate()
        {
            // Arrange
            await _fixture.RevokeCertificateAuthority();
            var certificate = await _fixture.GetSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            // The intermediary is revoked, thus the end certificate should have an unknown
            // revocation status that is offline.
            var flags = X509ChainStatusFlags.Revoked |
                        X509ChainStatusFlags.RevocationStatusUnknown |
                        X509ChainStatusFlags.OfflineRevocation;

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(flags, result.StatusFlags);
            Assert.Null(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }
    }
}
