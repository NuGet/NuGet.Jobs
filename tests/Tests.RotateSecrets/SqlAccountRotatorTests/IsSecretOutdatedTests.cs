using System;
using System.Collections.Generic;
using System.Linq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class IsSecretOutdatedTests : BaseTest
    {
        public static IEnumerable<object[]> IsSecretOutdatedData
        {
            get
            {
                var possibleRankTypeTuples = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                                              from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                                              select
                                              Tuple.Create(rank, type)).ToList();

                return TestUtils.GetSubsetsOf(possibleRankTypeTuples).Select(subset => new[] { subset });
            }
        }

        [Theory]
        [MemberData(nameof(IsSecretOutdatedData))]
        public void IsSecretOutdated(IEnumerable<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType>> outdatedSecrets)
        {
            var sqlAccountSecrets = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                                     from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                                     select
                                     TestUtils.CreateMockSqlAccountSecret(rank, type, isOutdated: outdatedSecrets.Contains(Tuple.Create(rank, type))).Object).ToList();

            var primaryAccountIsOutdated =
                outdatedSecrets.Contains(Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username)) &&
                outdatedSecrets.Contains(Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password));

            Assert.Equal(
                primaryAccountIsOutdated ? SecretRotator.TaskResult.Outdated : SecretRotator.TaskResult.Recent,
                TestUtils.CreateMockSqlAccountRotator(sqlAccountSecrets).Object.IsSecretOutdated());
        }
    }
}
