﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Logging;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using Test.Utility.Signing;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;
using NuGetHashAlgorithmName = NuGet.Common.HashAlgorithmName;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    [Collection(CertificateIntegrationTestCollection.Name)]
    public class SignatureValidatorIntegrationTests : IDisposable
    {
        private readonly CertificateIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly PackageSigningStateService _packageSigningStateService;
        private readonly Mock<ICertificateStore> _certificateStore;
        private readonly Mock<IValidationEntitiesContext> _validationEntitiesContext;
        private readonly SignaturePartsExtractor _signaturePartsExtractor;
        private readonly Mock<IProcessorPackageFileService> _packageFileService;
        private readonly Uri _nupkgUri;
        private readonly Mock<ICorePackageService> _corePackageService;
        private readonly IPackageSignatureVerifier _minimalPackageSignatureVerifier;
        private readonly IPackageSignatureVerifier _fullPackageSignatureVerifier;
        private readonly Mock<ITelemetryClient> _telemetryClient;
        private readonly TelemetryService _telemetryService;
        private readonly RecordingLogger<SignatureValidator> _logger;
        private readonly int _packageKey;
        private Stream _packageStream;
        private byte[] _savedPackageBytes;
        private SignatureValidationMessage _message;
        private readonly CancellationToken _token;
        private readonly SignatureValidator _target;

        public SignatureValidatorIntegrationTests(CertificateIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));

            _validationEntitiesContext = new Mock<IValidationEntitiesContext>();
            _validationEntitiesContext
                .Setup(x => x.PackageSigningStates)
                .Returns(DbSetMockFactory.Create<PackageSigningState>());
            _validationEntitiesContext
                .Setup(x => x.ParentCertificates)
                .Returns(DbSetMockFactory.Create<ParentCertificate>());
            _validationEntitiesContext
                .Setup(x => x.EndCertificates)
                .Returns(DbSetMockFactory.Create<EndCertificate>());
            _validationEntitiesContext
                .Setup(x => x.CertificateChainLinks)
                .Returns(DbSetMockFactory.Create<CertificateChainLink>());
            _validationEntitiesContext
                .Setup(x => x.PackageSignatures)
                .Returns(DbSetMockFactory.Create<PackageSignature>());
            _validationEntitiesContext
                .Setup(x => x.TrustedTimestamps)
                .Returns(DbSetMockFactory.Create<TrustedTimestamp>());

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddXunit(output);

            _packageSigningStateService = new PackageSigningStateService(
                _validationEntitiesContext.Object,
                loggerFactory.CreateLogger<PackageSigningStateService>());

            _certificateStore = new Mock<ICertificateStore>();

            _signaturePartsExtractor = new SignaturePartsExtractor(
                _certificateStore.Object,
                _validationEntitiesContext.Object,
                loggerFactory.CreateLogger<SignaturePartsExtractor>());

            _packageFileService = new Mock<IProcessorPackageFileService>();
            _nupkgUri = new Uri("https://example-storage/TestProcessor/b777135f-1aac-4ec2-a3eb-1f64fe1880d5/nuget.versioning.4.3.0.nupkg");
            _packageFileService
                .Setup(x => x.GetReadAndDeleteUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(() => _nupkgUri);
            _packageFileService
                .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string, Guid, Stream>((_, __, ___, s) =>
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        s.Position = 0;
                        s.CopyTo(memoryStream);
                        _savedPackageBytes = memoryStream.ToArray();
                    }
                });

            _corePackageService = new Mock<ICorePackageService>();

            // These dependencies are concrete.
            _minimalPackageSignatureVerifier = PackageSignatureVerifierFactory.CreateMinimal();
            _fullPackageSignatureVerifier = PackageSignatureVerifierFactory.CreateFull();

            _telemetryClient = new Mock<ITelemetryClient>();
            _telemetryService = new TelemetryService(_telemetryClient.Object);

            _logger = new RecordingLogger<SignatureValidator>(loggerFactory.CreateLogger<SignatureValidator>());

            // Initialize data.
            _packageKey = 23;
            _message = new SignatureValidationMessage(
                "SomePackageId",
                "1.2.3",
                new Uri("https://example/validation/somepackageid.1.2.3.nupkg"),
                new Guid("8eb5affc-2d0e-4315-9b79-5a194d39ebd1"));
            _token = CancellationToken.None;

            // Initialize the subject of testing.
            _target = new SignatureValidator(
                _packageSigningStateService,
                _minimalPackageSignatureVerifier,
                _fullPackageSignatureVerifier,
                _signaturePartsExtractor,
                _packageFileService.Object,
                _corePackageService.Object,
                _telemetryService,
                _logger);
        }

        public async Task<MemoryStream> GetSignedPackageStream1Async()
        {
            AllowSigningCertificate(_message.PackageId, await _fixture.GetSigningCertificateThumbprintAsync());
            return await _fixture.GetSignedPackageStream1Async(_output);
        }

        [Fact]
        public async Task AcceptsValidSignedPackage()
        {
            // Arrange
            _packageStream = await GetSignedPackageStream1Async();

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public async Task RejectsUntrustedSigningCertificate()
        {
            // Arrange
            AllowSigningCertificate(TestResources.SignedPackageLeafId, TestResources.Leaf1Thumbprint);
            _packageStream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);

            _message = new SignatureValidationMessage(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                Guid.NewGuid());

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3012", clientIssue.ClientCode);
            Assert.Equal(
                "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.",
                clientIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectsUntrustedTimestampingCertificate()
        {
            // Arrange
            var testServer = await _fixture.GetTestServerAsync();
            var untrustedRootCa = CertificateAuthority.Create(testServer.Url);
            var untrustedRootCertficate = new X509Certificate2(untrustedRootCa.Certificate.GetEncoded());
            var timestampService = TimestampService.Create(untrustedRootCa);
            using (testServer.RegisterDefaultResponders(timestampService))
            {
                byte[] packageBytes;
                using (var temporaryTrust = new TrustedTestCert<X509Certificate2>(
                    untrustedRootCertficate,
                    x => x,
                    StoreName.Root,
                    StoreLocation.LocalMachine))
                {
                    packageBytes = await _fixture.GenerateSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);
                }

                AllowSigningCertificate(
                    TestResources.SignedPackageLeafId,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _packageStream = new MemoryStream(packageBytes);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
                Assert.Equal("NU3028", clientIssue.ClientCode);
                Assert.Equal(
                    "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.",
                    clientIssue.ClientMessage);
            }
        }

        [Fact]
        public async Task AcceptsTrustedTimestampingCertificateWithUnavailableRevocation()
        {
            // Arrange
            var testServer = await _fixture.GetTestServerAsync();
            var trustedRootCa = CertificateAuthority.Create(testServer.Url);
            var trustedRootCertficate = new X509Certificate2(trustedRootCa.Certificate.GetEncoded());
            var timestampService = TimestampService.Create(trustedRootCa);
            using (var trust = new TrustedTestCert<X509Certificate2>(
                trustedRootCertficate,
                x => x,
                StoreName.Root,
                StoreLocation.LocalMachine))
            {
                byte[] packageBytes;
                using (testServer.RegisterDefaultResponders(timestampService))
                {
                    packageBytes = await _fixture.GenerateSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);
                }

                // Wait one second for the OCSP response cached by the operating system during signing to get stale.
                // This can be mitigated by leaving the OCSP unavailable during signing once this work item is done:
                // https://github.com/NuGet/Home/issues/6508
                await Task.Delay(TimeSpan.FromSeconds(1));

                AllowSigningCertificate(
                    TestResources.SignedPackageLeafId,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _packageStream = new MemoryStream(packageBytes);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                SignatureValidatorResult result;
                using (testServer.RegisterResponders(timestampService, addOcsp: false))
                {
                    // Act
                    result = await _target.ValidateAsync(
                       _packageKey,
                       _packageStream,
                       _message,
                       _token);
                }

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains("NU3028: The revocation function was unable to check revocation because the revocation server was offline.", allMessages);
                Assert.Contains("NU3028: The revocation function was unable to check revocation for the certificate.", allMessages);
            }
        }

        [Fact(Skip = "Appears to be flaky")]
        public async Task AcceptsTrustedSigningCertificateWithUnavailableRevocation()
        {
            // Arrange
            var testServer = await _fixture.GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());
            var signingCertificate = _fixture.CreateSigningCertificate(intermediateCa);
            using (var trust = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                x => x,
                StoreName.Root,
                StoreLocation.LocalMachine))
            {
                byte[] packageBytes;
                using (testServer.RegisterResponders(intermediateCa))
                {
                    packageBytes = await _fixture.GenerateSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        signingCertificate,
                        await _fixture.GetTimestampServiceUrlAsync(),
                        _output);
                }

                // Wait one second for the OCSP response cached by the operating system during signing to get stale.
                // This can be mitigated by leaving the OCSP unavailable during signing once this work item is done:
                // https://github.com/NuGet/Home/issues/6508
                await Task.Delay(TimeSpan.FromSeconds(1));

                AllowSigningCertificate(TestResources.SignedPackageLeafId, signingCertificate.ComputeSHA256Thumbprint());

                _packageStream = new MemoryStream(packageBytes);

                SignatureValidatorResult result;
                using (testServer.RegisterResponders(intermediateCa, addOcsp: false))
                {
                    // Act
                    result = await _target.ValidateAsync(
                       _packageKey,
                       _packageStream,
                       _message,
                       _token);
                }

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains("NU3018: The revocation function was unable to check revocation because the revocation server was offline.", allMessages);
                Assert.Contains("NU3018: The revocation function was unable to check revocation for the certificate.", allMessages);
            }
        }

        [Fact]
        public async Task RejectsPackageWithAddedFile()
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.CreateEntry("new-file.txt").Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.WriteLine("These contents were added after the package was signed.");
                }

                _packageStream = packageStream;
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsPackageWithModifiedFile()
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry("TestSigned.leaf-1.nuspec").Open())
                {
                    entryStream.Seek(0, SeekOrigin.End);
                    var extraBytes = Encoding.ASCII.GetBytes(Environment.NewLine);
                    entryStream.Write(extraBytes, 0, extraBytes.Length);
                }

                _packageStream = packageStream;
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsInvalidSignedCms()
        {
            // Arrange
            SetSignatureFileContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                Encoding.ASCII.GetBytes("This is not a valid signed CMS."));

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3003", typedIssue.ClientCode);
            Assert.Equal("The package signature is invalid or cannot be verified on this platform.", typedIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectsMultipleSignatures()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                configuredSignedCms: signedCms =>
                {
                    using (var additionalCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowSigningCertificate(
                            TestResources.SignedPackageLeafId,
                            additionalCertificate.ComputeSHA256Thumbprint());
                        signedCms.ComputeSignature(new CmsSigner(additionalCertificate));
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3009", typedIssue.ClientCode);
            Assert.Equal("The package signature file does not contain exactly one primary signature.", typedIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectsAuthorCounterSignatures()
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowSigningCertificate(_message.PackageId, counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.AuthorCounterSignaturesNotSupported, issue.IssueCode);
        }

        [Fact]
        public async Task StripsRepositoryCounterSignatures()
        {
            // Arrange
            var packageBytes = await _fixture.GenerateSignedPackageBytesAsync(
                await GetSignedPackageStream1Async(),
                new RepositorySignPackageRequest(
                    await _fixture.GetSigningCertificateAsync(),
                    NuGetHashAlgorithmName.SHA256,
                    NuGetHashAlgorithmName.SHA256,
                    new Uri("https://example-source/v3/index.json"),
                    new[] { "nuget", "microsoft" }),
                await _fixture.GetTimestampServiceUrlAsync(),
                _output);
            var packageStream = new MemoryStream(packageBytes);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            Assert.Empty(result.Issues);
            Assert.Equal(_nupkgUri, result.NupkgUri);
            Assert.NotNull(_savedPackageBytes);
            using (var savedPackageStream = new MemoryStream(_savedPackageBytes))
            using (var packageReader = new SignedPackageArchive(savedPackageStream, Stream.Null))
            {
                Assert.Equal("TestSigned.leaf-1", packageReader.NuspecReader.GetId());
                Assert.Equal("1.0.0", packageReader.NuspecReader.GetVersion().ToNormalizedString());
                Assert.True(await packageReader.IsSignedAsync(CancellationToken.None), "The package should still be signed.");
                var signature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                Assert.Equal(SignatureType.Author, signature.Type);
                Assert.Empty(signature.SignedCms.SignerInfos[0].CounterSignerInfos);
            }
        }

        [Fact]
        public async Task StripsRepositorySignatures()
        {
            // Arrange
            var packageBytes = await _fixture.GenerateSignedPackageBytesAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                new RepositorySignPackageRequest(
                    await _fixture.GetSigningCertificateAsync(),
                    NuGetHashAlgorithmName.SHA256,
                    NuGetHashAlgorithmName.SHA256,
                    new Uri("https://example-source/v3/index.json"),
                    new[] { "nuget", "microsoft" }),
                await _fixture.GetTimestampServiceUrlAsync(),
                _output);
            var packageStream = new MemoryStream(packageBytes);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.Equal(_nupkgUri, result.NupkgUri);
            Assert.NotNull(_savedPackageBytes);
            using (var savedPackageStream = new MemoryStream(_savedPackageBytes))
            using (var packageReader = new SignedPackageArchive(savedPackageStream, Stream.Null))
            {
                Assert.Equal("TestUnsigned", packageReader.NuspecReader.GetId());
                Assert.Equal("1.0.0", packageReader.NuspecReader.GetVersion().ToNormalizedString());
                Assert.False(await packageReader.IsSignedAsync(CancellationToken.None), "The package should no longer be signed.");
            }
        }

        [Theory]
        [InlineData(new[] { SignatureType.Author, SignatureType.Repository })]
        [InlineData(new[] { SignatureType.Repository, SignatureType.Author })]
        public async Task RejectsMutuallyExclusiveCounterSignaturesCommitmentTypes(SignatureType[] counterSignatureTypes)
        {
            // Arrange
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        AllowSigningCertificate(_message.PackageId, counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        foreach (var type in counterSignatureTypes)
                        {
                            cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(type));
                        }

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", typedIssue.ClientMessage);
        }

        [Theory]
        [InlineData("MA0GCyqGSIb3DQEJEAYD")] // base64 of ASN.1 encoded "1.2.840.113549.1.9.16.6.3" OID.
        [InlineData(null)] // No commitment type.
        public async Task AllowsNonAuthorAndRepositoryCounterSignatures(string commitmentTypeOidBase64)
        {
            // Arrange
            _message = new SignatureValidationMessage(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                Guid.NewGuid());
            var packageStream = await GetSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        var cmsSigner = new CmsSigner(counterCertificate);

                        if (commitmentTypeOidBase64 != null)
                        {
                            var value = new AsnEncodedData(
                                Oids.CommitmentTypeIndication,
                                Convert.FromBase64String(commitmentTypeOidBase64));

                            var attribute = new CryptographicAttributeObject(
                                new Oid(Oids.CommitmentTypeIndication),
                                new AsnEncodedDataCollection(value));

                            cmsSigner.SignedAttributes.Add(attribute);
                        }

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);

            // This failure type indicates the counter signature validation passed.
            VerifyNU3008(result);
        }

        [Fact]
        public async Task RejectsInvalidSignatureContent()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "!!--:::FOO...");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The package signature content is invalid.", typedIssue.ClientMessage);
        }

        [Fact]
        public async Task RejectInvalidSignatureContentVersion()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "Version:2" + Environment.NewLine + Environment.NewLine + "2.16.840.1.101.3.4.2.1-Hash:hash");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlySignatureFormatVersion1Supported, issue.IssueCode);
        }

        [Fact]
        public async Task RejectsNonAuthorSignature()
        {
            // Arrange
            var content = new SignatureContent(
                SigningSpecifications.V1,
                NuGetHashAlgorithmName.SHA256,
                hashValue: "hash");
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                content.GetBytes());

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlyAuthorSignaturesSupported, issue.IssueCode);
        }

        [Fact]
        public async Task RejectsZip64Packages()
        {
            // Arrange
            _packageStream = TestResources.GetResourceStream(TestResources.Zip64Package);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.PackageIsZip64, issue.IssueCode);
        }

        private void SetSignatureFileContent(Stream packageStream, byte[] fileContent)
        {
            try
            {
                using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(packageStream))
                {
                    zipFile.IsStreamOwner = false;

                    zipFile.BeginUpdate();
                    zipFile.Delete(SigningSpecifications.V1.SignaturePath);
                    zipFile.CommitUpdate();
                    zipFile.BeginUpdate();
                    zipFile.Add(
                        new StreamDataSource(new MemoryStream(fileContent)),
                        SigningSpecifications.V1.SignaturePath,
                        CompressionMethod.Stored);
                    zipFile.CommitUpdate();
                }

                packageStream.Position = 0;

                _packageStream = packageStream;
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }
        }

        private void ModifySignatureContent(Stream packageStream, Action<SignedCms> configuredSignedCms = null)
        {
            SignedCms signedCms;
            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                {
                    var signature = PrimarySignature.Load(entryStream);
                    signedCms = signature.SignedCms;
                }
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            configuredSignedCms(signedCms);

            SetSignatureFileContent(packageStream, signedCms.Encode());
        }

        private void SetSignatureContent(
            string packageId,
            Stream packageStream,
            byte[] signatureContent = null,
            Action<SignedCms> configuredSignedCms = null)
        {
            if (signatureContent == null)
            {
                signatureContent = new SignatureContent(
                    SigningSpecifications.V1,
                    NuGetHashAlgorithmName.SHA256,
                    hashValue: "hash").GetBytes();
            }

            using (var certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
            {
                AllowSigningCertificate(packageId, certificate.ComputeSHA256Thumbprint());

                var contentInfo = new ContentInfo(signatureContent);
                var signedCms = new SignedCms(contentInfo);

                signedCms.ComputeSignature(new CmsSigner(certificate));

                configuredSignedCms?.Invoke(signedCms);

                var fileContent = signedCms.Encode();

                SetSignatureFileContent(packageStream, fileContent);
            }
        }

        private void SetSignatureContent(string packageId, Stream packageStream, string signatureContent)
        {
            SetSignatureContent(packageId, packageStream, signatureContent: Encoding.UTF8.GetBytes(signatureContent));
        }

        private void AllowSigningCertificate(string packageId, string thumbprint)
        {
            _corePackageService
                .Setup(x => x.IsSigningRequired(It.IsAny<string>()))
                .Returns(false);

            _corePackageService
                .Setup(x => x.IsAcceptableSigningCertificate(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string actualPackageId, string actualThumbprint) =>
                    actualPackageId == packageId && actualThumbprint == thumbprint);
        }

        private void VerifyPackageSigningStatus(SignatureValidatorResult result, ValidationStatus validationStatus, PackageSigningStatus packageSigningStatus)
        {
            Assert.Equal(validationStatus, result.State);
            var state = _validationEntitiesContext
                .Object
                .PackageSigningStates
                .Where(x => x.PackageKey == _packageKey)
                .SingleOrDefault();
            Assert.Equal(state.PackageId, _message.PackageId);
            Assert.Equal(state.PackageNormalizedVersion, _message.PackageVersion);
            Assert.Equal(state.SigningStatus, packageSigningStatus);
        }

        private static void VerifyNU3008(SignatureValidatorResult result)
        {
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3008", typedIssue.ClientCode);
            Assert.Equal("The package integrity check failed.", typedIssue.ClientMessage);
        }

        public void Dispose()
        {
            _packageStream?.Dispose();
        }

        private class StreamDataSource : IStaticDataSource
        {
            private readonly Stream _stream;

            public StreamDataSource(Stream stream)
            {
                _stream = stream;
            }

            public Stream GetSource()
            {
                return _stream;
            }
        }
    }
}