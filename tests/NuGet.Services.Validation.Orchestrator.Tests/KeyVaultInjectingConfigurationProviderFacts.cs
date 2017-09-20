// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Moq;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class KeyVaultInjectingConfigurationProviderFacts
    {
        [Fact]
        public void PassesThroughGetChildKeys()
        {
            var originalProviderMock = new Mock<IConfigurationProvider>();
            var secretInjectorMock = new Mock<ISecretInjector>(MockBehavior.Strict); // make sure no calls to this guy

            var keys = new string[] { "someKey" };
            var parentPath = "Section1";

            IConfigurationProvider provider = new KeyVaultInjectingConfigurationProvider(originalProviderMock.Object, secretInjectorMock.Object);
            provider.GetChildKeys(keys, parentPath);

            originalProviderMock.Verify(p => p.GetChildKeys(keys, parentPath), Times.Once());
            originalProviderMock.Verify(p => p.GetChildKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void PassesThroughGetReloadToken()
        {
            var originalProviderMock = new Mock<IConfigurationProvider>();
            var secretInjectorMock = new Mock<ISecretInjector>(MockBehavior.Strict);

            IConfigurationProvider provider = new KeyVaultInjectingConfigurationProvider(originalProviderMock.Object, secretInjectorMock.Object);
            provider.GetReloadToken();

            originalProviderMock.Verify(p => p.GetReloadToken(), Times.Once());
        }

        [Fact]
        public void PassesThroughLoad()
        {
            var originalProviderMock = new Mock<IConfigurationProvider>();
            var secretInjectorMock = new Mock<ISecretInjector>(MockBehavior.Strict);

            IConfigurationProvider provider = new KeyVaultInjectingConfigurationProvider(originalProviderMock.Object, secretInjectorMock.Object);
            provider.Load();

            originalProviderMock.Verify(p => p.Load(), Times.Once());
        }

        [Fact]
        public void PassesThroughSet()
        {
            var originalProviderMock = new Mock<IConfigurationProvider>();
            var secretInjectorMock = new Mock<ISecretInjector>(MockBehavior.Strict);

            var key = "SomeKey";
            var value = "SomeValue";

            IConfigurationProvider provider = new KeyVaultInjectingConfigurationProvider(originalProviderMock.Object, secretInjectorMock.Object);
            provider.Set(key, value);

            originalProviderMock.Verify(p => p.Set(key, value), Times.Once());
            originalProviderMock.Verify(p => p.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void InjectsSecrets()
        {
            var originalProviderMock = new Mock<IConfigurationProvider>(MockBehavior.Strict);
            var secretInjectorMock = new Mock<ISecretInjector>(MockBehavior.Strict);

            const string key = "SomeKey";
            const string uninjectedValue = "Value=$$Secret$$";
            const string injectedValue = "Value=SecretValue";

            var originalOutValue = uninjectedValue;
            originalProviderMock.Setup(p => p.TryGet(key, out originalOutValue)).Returns(true).Verifiable();
            secretInjectorMock.Setup(i => i.InjectAsync(uninjectedValue)).ReturnsAsync(injectedValue).Verifiable();

            IConfigurationProvider provider = new KeyVaultInjectingConfigurationProvider(originalProviderMock.Object, secretInjectorMock.Object);
            var found = provider.TryGet(key, out string value);

            Assert.True(found);
            Assert.Equal(injectedValue, value);

            originalProviderMock.Verify(p => p.TryGet(key, out originalOutValue), Times.Once());
            secretInjectorMock.Verify(i => i.InjectAsync(uninjectedValue), Times.Once());
        }
    }
}
