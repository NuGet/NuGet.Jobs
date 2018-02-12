// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    [CollectionDefinition(Name)]
    public class RevokedParentCertificateIntegrationTestCollection : ICollectionFixture<CertificateIntegrationTestFixture>
    {
        public const string Name = "Revoked Parent Certificate integration test collection";
    }
}
