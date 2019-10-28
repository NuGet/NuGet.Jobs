// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ess;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using Test.Utility.Signing;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class SignatureFormatValidatorFacts
    {
        private readonly ProcessSignatureConfiguration _configuration;
        private readonly Mock<IOptionsSnapshot<ProcessSignatureConfiguration>> _optionsSnapshot;
        private readonly SignatureFormatValidator _formatValidator;

        public SignatureFormatValidatorFacts()
        {
            _configuration = new ProcessSignatureConfiguration();
            _optionsSnapshot = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
            _optionsSnapshot.Setup(x => x.Value).Returns(() => _configuration);
            _formatValidator = new SignatureFormatValidator(_optionsSnapshot.Object);
        }

        [Fact]
        public void ValidateSigningCertificateUsage_WhenTimestampIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => _formatValidator.ValidateSigningCertificateUsage(timestamp: null));

            Assert.Equal("timestamp", exception.ParamName);
        }

        [Theory]
        [InlineData(SigningCertificateUsage.None)]
        [InlineData(SigningCertificateUsage.V1)]
        [InlineData(SigningCertificateUsage.V2)]
        [InlineData(SigningCertificateUsage.V1 | SigningCertificateUsage.V2)]
        public void ValidateSigningCertificateUsage_WithValidInput_ReturnsExpectedResult(SigningCertificateUsage expectedResult)
        {
            Timestamp timestamp = GenerateTimestamp(expectedResult);
            SigningCertificateUsage actualResult = _formatValidator.ValidateSigningCertificateUsage(timestamp);

            Assert.Equal(expectedResult, actualResult);
        }

        private static Timestamp GenerateTimestamp(SigningCertificateUsage signingCertificateUsage)
        {
            using (X509Certificate2 certificate = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: false))
            {
                // Most of the following values are dummy values since the tests do not require a valid, trusted
                // timestamping certificate.
                var hashAlgorithm = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
                    NistObjectIdentifiers.IdSha256,
                    DerNull.Instance);
                var tstInfo = new Org.BouncyCastle.Asn1.Tsp.TstInfo(
                    tsaPolicyId: new DerObjectIdentifier("1.2.3"),
                    messageImprint: new Org.BouncyCastle.Asn1.Tsp.MessageImprint(hashAlgorithm, new byte[32]),
                    serialNumber: new DerInteger(1),
                    genTime: new DerGeneralizedTime(DateTime.UtcNow),
                    accuracy: new Org.BouncyCastle.Asn1.Tsp.Accuracy(new DerInteger(1), millis: null, micros: null),
                    ordering: DerBoolean.True,
                    nonce: new DerInteger(1),
                    tsa: new Org.BouncyCastle.Asn1.X509.GeneralName(new X509Name("CN=NuGet")),
                    extensions: null);
                var contentInfo = new ContentInfo(new Oid(Oids.TSTInfoContentType), tstInfo.GetDerEncoded());
                var signedCms = new SignedCms(contentInfo);
                var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, certificate);

                if (signingCertificateUsage.HasFlag(SigningCertificateUsage.V1))
                {
                    byte[] hash = DigestUtilities.CalculateDigest("SHA-1", certificate.RawData);
                    var signingCertificate = new Org.BouncyCastle.Asn1.Ess.SigningCertificate(new EssCertID(hash));
                    byte[] attributeValue = new DerSet(signingCertificate).GetEncoded();
                    var attribute = new AsnEncodedData(new Oid(Oids.SigningCertificate), attributeValue);

                    cmsSigner.SignedAttributes.Add(attribute);
                }

                if (signingCertificateUsage.HasFlag(SigningCertificateUsage.V2))
                {
                    byte[] hash = DigestUtilities.CalculateDigest("SHA-256", certificate.RawData);
                    var signingCertificateV2 = new Org.BouncyCastle.Asn1.Ess.SigningCertificateV2(new EssCertIDv2(hash));
                    byte[] attributeValue = new DerSet(signingCertificateV2).GetEncoded();
                    var attribute = new AsnEncodedData(new Oid(Oids.SigningCertificateV2), attributeValue);

                    cmsSigner.SignedAttributes.Add(attribute);
                }

                signedCms.ComputeSignature(cmsSigner);

                return new Timestamp(signedCms);
            }
        }
    }
}