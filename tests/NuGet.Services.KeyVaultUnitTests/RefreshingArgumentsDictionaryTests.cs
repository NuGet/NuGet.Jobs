// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Jobs;
using NuGet.Services.KeyVault;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            string value1 = await refreshingArgumentsDictionary.GetOrThrow<string>(nameOfSecret);
            value1 = await refreshingArgumentsDictionary.GetOrThrow<string>(nameOfSecret);

            // Assert
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Once);
            Assert.Equal(firstSecret, value1);

            // Arrange 2
            Thread.Sleep(delayBeforeRefreshingMs);
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns(Task.FromResult(secondSecret));

            // Act 2
            string value2 = await refreshingArgumentsDictionary.GetOrThrow<string>(nameOfSecret);
            value2 = await refreshingArgumentsDictionary.GetOrThrow<string>(nameOfSecret);

            // Assert 2
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(secondSecret, value2);
        }

        [Fact]
        public async void HandlesKeyNotFound()
        {
            var fakeKey = "not a real key";
            IArgumentsDictionary dummy = CreateDummyArgumentsDictionary();

            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.GetOrThrow<string>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.GetOrThrow<int>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.GetOrThrow<bool>(fakeKey));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => dummy.GetOrThrow<DateTime>(fakeKey));

            Assert.Equal(default(string), await dummy.GetOrDefault<string>(fakeKey));
            Assert.Equal(default(int), await dummy.GetOrDefault<int>(fakeKey));
            Assert.Equal(default(bool), await dummy.GetOrDefault<bool>(fakeKey));
            Assert.Equal(default(DateTime), await dummy.GetOrDefault<DateTime>(fakeKey));
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(bool))]
        [InlineData(typeof(DateTime))]
        public async void HandlesNullOrEmptyArgument(Type type)
        {
            // Arrange
            IArgumentsDictionary dummy = CreateDummyArgumentsDictionary();

            var getOrThrowMI = typeof(IArgumentsDictionary).GetMethod("GetOrThrow").MakeGenericMethod(type);
            var getOrDefaultMI = typeof(IArgumentsDictionary).GetMethod("GetOrDefault").MakeGenericMethod(type);

            Type[] taskTypeArgs = { type };
            var taskType = typeof(Task<>).MakeGenericType(taskTypeArgs);

            var defaultOfType = GetDefault(type);

            var nullKey = "this key has a null value";
            dummy.Add(nullKey, null);
            object[] nullKeyThrowArgs = { nullKey };
            object[] nullKeyDefaultArgs = { nullKey, defaultOfType };

            var emptyKey = "this key has an empty value";
            dummy.Add(emptyKey, "");
            object[] emptyKeyThrowArgs = { emptyKey };
            object[] emptyKeyDefaultArgs = { emptyKey, defaultOfType };

            // Act and Assert

            await Assert.ThrowsAsync<ArgumentNullException>(() => (Task)getOrThrowMI.Invoke(dummy, nullKeyThrowArgs));
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMI.Invoke(dummy, nullKeyDefaultArgs));

            await Assert.ThrowsAsync<ArgumentNullException>(() => (Task)getOrThrowMI.Invoke(dummy, emptyKeyThrowArgs));
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMI.Invoke(dummy, emptyKeyDefaultArgs));
        }

        public dynamic GetDefault(Type t)
        {
            return this.GetType().GetMethod("GetDefaultGeneric").MakeGenericMethod(t).Invoke(this, null);
        }

        public T GetDefaultGeneric<T>()
        {
            return default(T);
        }

        private IArgumentsDictionary CreateDummyArgumentsDictionary()
        {
            return new RefreshingArgumentsDictionary(new SecretReaderFactory().CreateSecretInjector(new EmptySecretReader()), new Dictionary<string, string>());
        }
    }
}
