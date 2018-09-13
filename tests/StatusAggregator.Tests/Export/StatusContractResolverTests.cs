// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StatusAggregator.Export;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class StatusContractResolverTests
    {
        public class HandlesStringsProperly : StatusContractResolverTest
        {
            [Fact]
            public void SerializesStringWithContents()
            {
                var test = new StringTest("value");

                var result = GetResult(test);

                var property = result.Property(nameof(StringTest.Value));
                Assert.NotNull(property);
                Assert.Equal(test.Value, property.Value.Value<string>());
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void DoesNotSerializeNullOrEmptyString(string value)
            {
                var test = new StringTest(value);

                var result = GetResult(test);

                Assert.Null(result.Property(nameof(StringTest.Value)));
            }

            public class StringTest
            {
                public string Value { get; }

                public StringTest(string value)
                {
                    Value = value;
                }
            }
        }

        public class HandlesEnumerablesProperly : StatusContractResolverTest
        {
            [Fact]
            public void SerializesEnumerableWithContents()
            {
                var array = new string[] { "one", "two", "three" };
                var test = new EnumerableTest(array);

                var result = GetResult(test);

                AssertEnumerableIdentical(
                    array, 
                    result.Property(nameof(EnumerableTest.Enumerable)).Value as JArray);
                AssertEnumerableIdentical(
                    array,
                    result.Property(nameof(EnumerableTest.GenericEnumerable)).Value as JArray);
                AssertEnumerableIdentical(
                    array,
                    result.Property(nameof(EnumerableTest.Array)).Value as JArray);
            }

            [Fact]
            public void DoesNotSerializeNullEnumerable()
            {
                var test = new EnumerableTest(null);

                var result = GetResult(test);

                Assert.Null(result.Property(nameof(EnumerableTest.Enumerable)));
                Assert.Null(result.Property(nameof(EnumerableTest.GenericEnumerable)));
                Assert.Null(result.Property(nameof(EnumerableTest.Array)));
            }

            [Fact]
            public void DoesNotSerializeEmptyEnumerable()
            {
                var array = new string[0];
                var test = new EnumerableTest(array);

                var result = GetResult(test);

                Assert.Null(result.Property(nameof(EnumerableTest.Enumerable)));
                Assert.Null(result.Property(nameof(EnumerableTest.GenericEnumerable)));
                Assert.Null(result.Property(nameof(EnumerableTest.Array)));
            }

            public class EnumerableTest
            {
                public IEnumerable Enumerable { get; }
                public IEnumerable<string> GenericEnumerable { get; }
                public string[] Array { get; }

                public EnumerableTest(string[] array)
                {
                    Enumerable = array;
                    GenericEnumerable = array;
                    Array = array;
                }
            }

            private void AssertEnumerableIdentical(IEnumerable<string> expected, JArray actual)
            {
                Assert.Equal(expected.Count(), actual.Children().Count());
                for (var i = 0; i < expected.Count(); i++)
                {
                    Assert.Equal(expected.ElementAt(i), actual.ElementAt(i).Value<string>());
                }
            }
        }

        public class StatusContractResolverTest
        {
            public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new StatusContractResolver(),
            };

            public JObject GetResult(object input)
            {
                var jsonString = JsonConvert.SerializeObject(input, JsonSerializerSettings);
                return JObject.Parse(jsonString);
            }
        }
    }
}
