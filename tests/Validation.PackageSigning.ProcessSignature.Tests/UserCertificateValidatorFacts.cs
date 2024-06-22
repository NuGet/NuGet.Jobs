// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Services.Entities;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class UserCertificateValidatorFacts
    {
        public class TheIsAcceptableSigningCertificateMethod
        {
            private readonly Package _package;
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user;
            private readonly Certificate _certificate;
            private readonly X509Certificate2 _x509Certificate1;
            private readonly X509Certificate2 _x509Certificate2;
            private readonly UserCertificateValidator _target;

            public TheIsAcceptableSigningCertificateMethod()
            {
                var signature1 = TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1).Result;
                var signature2 = TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf2).Result;
                _x509Certificate1 = signature1.SignerInfo.Certificate;
                _x509Certificate2 = signature2.SignerInfo.Certificate;

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
                _package = new Package()
                {
                    Key = 3,
                    PackageRegistration = _packageRegistration
                };
                _certificate = new Certificate()
                {
                    Key = 4,
                    Thumbprint = _x509Certificate1.ComputeSHA256Thumbprint(),
                };

                _packageRegistration.Owners.Add(_user);

                _target = new UserCertificateValidator(Mock.Of<ILogger<UserCertificateValidator>>());
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenPackageRegistrationIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => _target.IsAcceptableSigningCertificate(packageRegistration: null, _x509Certificate1));

                Assert.Equal("packageRegistration", exception.ParamName);
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WhenThumbprintIsInvalid_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => _target.IsAcceptableSigningCertificate(_packageRegistration, certificate: null));

                Assert.Equal("certificate", exception.ParamName);
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNoCertificate_ReturnsFalse()
            {
                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasNonMatchingCertificate_ReturnsFalse()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsNullAndOwnerHasMatchingCertificate_ReturnsTrue()
            {
                _user.UserCertificates.Add(new UserCertificate()
                {
                    Key = 5,
                    User = _user,
                    UserKey = _user.Key,
                    Certificate = _certificate,
                    CertificateKey = _certificate.Key
                });

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOwnerHasMatchingCertificate_ReturnsTrue()
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

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsNullAndOnlyOtherOwnerHasMatchingCertificate_ReturnsTrue()
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

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNoCertificate_ReturnsFalse()
            {
                _packageRegistration.RequiredSigners.Add(_user);

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithOneOwner_WhenRequiredSignerIsOwnerAndOwnerHasMatchingCertificate_ReturnsTrue()
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

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOwnerHasMatchingCertificate_ReturnsTrue()
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

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndOnlyOtherOwnerHasMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveNonMatchingCertificate_ReturnsFalse()
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

                Assert.False(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate2));
            }

            [Fact]
            public void IsAcceptableSigningCertificate_WithTwoOwners_WhenRequiredSignerIsOwnerAndAllOwnersHaveMatchingCertificate_ReturnsTrue()
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

                Assert.True(_target.IsAcceptableSigningCertificate(_packageRegistration, _x509Certificate1));
            }
        }
    }
}
