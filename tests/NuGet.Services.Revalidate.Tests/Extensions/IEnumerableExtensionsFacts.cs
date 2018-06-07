// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Extensions
{
    public class IEnumerableExtensionsFacts
    {
        [Theory]
        [MemberData(nameof(TheBatchMethodData))]
        public void TheBatchMethod(int[] input, int batchSize, List<List<int>> expected)
        {
            var actual = input.Batch(batchSize);

            AssertEqualBatches(expected, actual);
        }

        public static IEnumerable<object[]> TheBatchMethodData()
        {
            yield return new object[]
            {
                new[] { 1, 2, 3, },
                3,
                new List<List<int>>
                {
                    new List<int> { 1, 2, 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                2,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 }
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                1,
                new List<List<int>>
                {
                    new List<int> { 1 },
                    new List<int> { 2 },
                    new List<int> { 3 }
                }
            };
        }

        [Theory]
        [MemberData(nameof(TheWeightedBatchMethodData))]
        public void TheWeightedBatchMethod(int[] input, int batchSize, List<List<int>> expected)
        {
            // Use each element's value as its weight
            var actual = input.WeightedBatch(batchSize, i => i);

            AssertEqualBatches(expected, actual);
        }

        public static IEnumerable<object[]> TheWeightedBatchMethodData()
        {
            yield return new object[]
            {
                new[] { 1, 2, 3, },
                6,
                new List<List<int>>
                {
                    new List<int> { 1, 2, 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                5,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                3,
                new List<List<int>>
                {
                    new List<int> { 1, 2 },
                    new List<int> { 3 },
                }
            };

            yield return new object[]
            {
                new[] { 1, 2, 3, },
                2,
                new List<List<int>>
                {
                    new List<int> { 1 },
                    new List<int> { 2 },
                    new List<int> { 3 }
                }
            };
        }

        private void AssertEqualBatches(List<List<int>> expected, List<List<int>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }
}
