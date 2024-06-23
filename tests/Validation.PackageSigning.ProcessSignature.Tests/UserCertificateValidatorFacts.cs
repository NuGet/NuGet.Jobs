// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Issues;
using Xunit;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class UserCertificateValidatorFacts
    {
        public class TheValidateCertificateMethod
        {
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user;
            private readonly Certificate _certificate;
            private readonly X509Certificate2 _x509Certificate1;
            private readonly X509Certificate2 _x509Certificate2;
            private readonly string _x509Certificate2Thumprint;
            private readonly X509Certificate2Collection _collection;
            private readonly UserCertificateValidator _target;
            private readonly X509Certificate2 _x509CertificateAzureTrustedSigning;
            private readonly string _x509CertificateAzureTrustedSigningThumbprint;
            private readonly X509Certificate2Collection _collectionAzureTrustedSigning;

            public TheValidateCertificateMethod(ITestOutputHelper output)
            {
                var signature1 = TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1).Result;
                var signature2 = TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf2).Result;
                _x509Certificate1 = signature1.SignerInfo.Certificate;
                _x509Certificate2 = signature2.SignerInfo.Certificate;
                _x509Certificate2Thumprint = _x509Certificate2.ComputeSHA256Thumbprint();
                _collection = new X509Certificate2Collection();

                var azureTrustedSigningSignature = TestResources.LoadPrimarySignatureAsync(TestResources.AzureTrustedSigningSignedPackage).Result;
                _x509CertificateAzureTrustedSigning = azureTrustedSigningSignature.SignerInfo.Certificate;
                _x509CertificateAzureTrustedSigningThumbprint = _x509CertificateAzureTrustedSigning.ComputeSHA256Thumbprint();
                _collectionAzureTrustedSigning = azureTrustedSigningSignature.SignedCms.Certificates;

                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _packageRegistration = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                _certificate = new Certificate()
                {
                    Key = 4,
                    Thumbprint = _x509Certificate1.ComputeSHA256Thumbprint(),
                };

                _packageRegistration.Owners.Add(_user);

                var logger = new LoggerFactory()
                    .AddXunit(output)
                    .CreateLogger<UserCertificateValidator>();
                _target = new UserCertificateValidator(logger);
            }

            [Fact]
            public void WhenPackageRegistrationIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => _target.ValidateCertificate(packageRegistration: null, _x509Certificate1, _collection));

                Assert.Equal("packageRegistration", exception.ParamName);
            }

            [Fact]
            public void WhenCertificateIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => _target.ValidateCertificate(_packageRegistration, certificate: null, _collection));

                Assert.Equal("certificate", exception.ParamName);
            }

            [Fact]
            public void WhenExtraStoreIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => _target.ValidateCertificate(_packageRegistration, _x509Certificate1, extraStore: null));

                Assert.Equal("extraStore", exception.ParamName);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNoCertificate_ReturnsIssue()
            {
                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);
                Assert.NotNull(issue);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasMatchingCertificate_ReturnsNoIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndCertificateIsMatchingAzureTrustedSigning_ReturnsNoIssue()
            {
                _user.UserCertificatePatterns.Add(new UserCertificatePattern
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    PatternType = CertificatePatternType.AzureTrustedSigning,
                    Identifier = TestResources.AzureTrustedSigningIdentifierOid,
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509CertificateAzureTrustedSigning, _collectionAzureTrustedSigning);

                Assert.Null(issue);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndCertificateIsMatchingAzureTrustedSigningPublicTrustMarker_ReturnsIssue()
            {
                _user.UserCertificatePatterns.Add(new UserCertificatePattern
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    PatternType = CertificatePatternType.AzureTrustedSigning,
                    Identifier = "1.3.6.1.4.1.311.97.1.0",
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509CertificateAzureTrustedSigning, _collectionAzureTrustedSigning);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedAzureTrustedSigningCertificateFailure>(issue);
                Assert.Equal(_x509CertificateAzureTrustedSigningThumbprint, unauthorized.Sha256Thumbprint);
                Assert.Equal(TestResources.AzureTrustedSigningIdentifierOid, unauthorized.EnhancedKeyUsageOid);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndCertificateIsNonMatchingAzureTrustedSigning_ReturnsNoIssue()
            {
                _user.UserCertificatePatterns.Add(new UserCertificatePattern
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    PatternType = CertificatePatternType.AzureTrustedSigning,
                    Identifier = TestResources.AzureTrustedSigningIdentifierOid + ".1",
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509CertificateAzureTrustedSigning, _collectionAzureTrustedSigning);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedAzureTrustedSigningCertificateFailure>(issue);
                Assert.Equal(_x509CertificateAzureTrustedSigningThumbprint, unauthorized.Sha256Thumbprint);
                Assert.Equal(TestResources.AzureTrustedSigningIdentifierOid, unauthorized.EnhancedKeyUsageOid);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndFingerprintIsNotMatching_ReturnsIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509CertificateAzureTrustedSigning, _collectionAzureTrustedSigning);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedAzureTrustedSigningCertificateFailure>(issue);
                Assert.Equal(_x509CertificateAzureTrustedSigningThumbprint, unauthorized.Sha256Thumbprint);
                Assert.Equal(TestResources.AzureTrustedSigningIdentifierOid, unauthorized.EnhancedKeyUsageOid);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsNullAndFingerprintIsMatching_ReturnsNoIssue()
            {
                _certificate.Thumbprint = _x509CertificateAzureTrustedSigningThumbprint;

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                var issue = _target.ValidateCertificate(_packageRegistration, _x509CertificateAzureTrustedSigning, _collectionAzureTrustedSigning);

                Assert.Null(issue);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasMatchingCertificate_ReturnsNoIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };

                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasMatchingCertificate_ReturnsNoIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNoCertificate_ReturnsIssue()
            {
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.NotNull(issue);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasMatchingCertificate_ReturnsNoIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasMatchingCertificate_ReturnsNoIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasMatchingCertificate_ReturnsIssue()
            {
                var otherOwner = new User()
                {
                    Key = 5,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 6,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.NotNull(issue);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveNonMatchingCertificate_ReturnsIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });
                var otherOwner = new User()
                {
                    Key = 6,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 7,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate2, _collection);

                Assert.NotNull(issue);
                var unauthorized = Assert.IsType<UnauthorizedCertificateSha256Failure>(issue);
                Assert.Equal(_x509Certificate2Thumprint, unauthorized.Sha256Thumbprint);
            }

            [Fact]
            public void WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveMatchingCertificate_ReturnsNoIssue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });
                var otherOwner = new User()
                {
                    Key = 6,
                    Username = "d"
                };
                otherOwner.UserCertificates.Add(new UserCertificate()
                {
                    Key = 7,
                    User = otherOwner,
                    UserKey = otherOwner.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                _packageRegistration.Owners.Add(otherOwner);
                _packageRegistration.RequiredSigners.Add(_user);

                var issue = _target.ValidateCertificate(_packageRegistration, _x509Certificate1, _collection);

                Assert.Null(issue);
            }
        }
    }
}
