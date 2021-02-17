// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.KeyVault;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class SharedAccessSignatureServiceFacts
    {
        [Fact]
        public void SecretInjectorNullThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new SharedAccessSignatureService(null));
        }

        [Fact]
        public async Task AlwaysReturnsSasToken()
        {
            var sasDefinition = "sasDefinition";
            var sasToken = "sasToken";
            var secretInjectorMock = new Mock<ISecretInjector>();
            secretInjectorMock
                .Setup(si => si.InjectAsync(It.Is<string>(sd => sd == $"$${sasDefinition}$$")))
                .ReturnsAsync(sasToken);
            
            var service = new SharedAccessSignatureService(secretInjectorMock.Object);
            var result = await service.GetFromManagedStorageAccountAsync(sasDefinition);

            Assert.Equal(result, sasToken);
        }
    }
}
