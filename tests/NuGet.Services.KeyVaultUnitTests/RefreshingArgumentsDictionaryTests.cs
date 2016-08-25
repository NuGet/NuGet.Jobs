// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Jobs;
using NuGet.Services.KeyVault;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.KeyVaultUnitTests
{
    public class RefreshingArgumentsDictionaryTests
    {
        [Fact]
        public async void RefreshesArgumentsAfterIntervalPasses()
        {
            // Arrange
            const string nameOfSecret = "hello i'm a secret";
            const string firstSecret = "secret1";
            const string secondSecret = "secret2";
            const int refreshIntervalSec = 1;
            const int delayBeforeRefreshingMs = (refreshIntervalSec + 1) * 1000;

            var mockSecretInjector = new Mock<ISecretInjector>();
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns(Task.FromResult(firstSecret));

            var unprocessedDictionary = new Dictionary<string, string>()
            {
                {RefreshingArgumentsDictionary.RefreshArgsIntervalSec, refreshIntervalSec.ToString()},
                {nameOfSecret, "fetch me from KeyVault pls"}
            };

            var refreshingArgumentsDictionary = new RefreshingArgumentsDictionary(mockSecretInjector.Object, unprocessedDictionary);

            // Act
            string value1 = await refreshingArgumentsDictionary.Get<string>(nameOfSecret);
            value1 = await refreshingArgumentsDictionary.Get<string>(nameOfSecret);

            // Assert
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(firstSecret, value1);

            // Arrange 2
            Thread.Sleep(delayBeforeRefreshingMs);
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns(Task.FromResult(secondSecret));

            // Act 2
            string value2 = await refreshingArgumentsDictionary.Get<string>(nameOfSecret);
            value2 = await refreshingArgumentsDictionary.Get<string>(nameOfSecret);

            // Assert 2
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(secondSecret, value2);
        }

        [Fact]
        public async void HandlesKeyNotFound()
        {
            var fakeKey = "not a real key";
            IArgumentsDictionary dummy = CreateDummyArgumentsDictionary();

            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.Get<string>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.Get<int>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.Get<bool>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.Get<DateTime>(fakeKey));

            Assert.Equal(default(string), await dummy.GetOrDefault<string>(fakeKey));
            Assert.Equal(default(int), await dummy.GetOrDefault<int>(fakeKey));
            Assert.Equal(default(bool), await dummy.GetOrDefault<bool>(fakeKey));
            Assert.Equal(default(DateTime), await dummy.GetOrDefault<DateTime>(fakeKey));
        }

        [Fact]
        public async void HandlesNullOrEmptyArgument()
        {
            IArgumentsDictionary dummy = CreateDummyArgumentsDictionary();

            var nullKey = "this key has a null value";
            dummy.Add(nullKey, null);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<string>(nullKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<int>(nullKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<bool>(nullKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<DateTime>(nullKey));

            Assert.Equal(default(string), await dummy.GetOrDefault<string>(nullKey));
            Assert.Equal(default(int), await dummy.GetOrDefault<int>(nullKey));
            Assert.Equal(default(bool), await dummy.GetOrDefault<bool>(nullKey));
            Assert.Equal(default(DateTime), await dummy.GetOrDefault<DateTime>(nullKey));

            var emptyKey = "this key has an empty value";
            dummy.Add(emptyKey, "");

            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<string>(emptyKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<int>(emptyKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<bool>(emptyKey));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.Get<DateTime>(emptyKey));

            Assert.Equal(default(string), await dummy.GetOrDefault<string>(emptyKey));
            Assert.Equal(default(int), await dummy.GetOrDefault<int>(emptyKey));
            Assert.Equal(default(bool), await dummy.GetOrDefault<bool>(emptyKey));
            Assert.Equal(default(DateTime), await dummy.GetOrDefault<DateTime>(emptyKey));
        }

        private IArgumentsDictionary CreateDummyArgumentsDictionary()
        {
            return new RefreshingArgumentsDictionary(new SecretReaderFactory().CreateSecretInjector(new EmptySecretReader()), new Dictionary<string, string>());
        }
    }
}
