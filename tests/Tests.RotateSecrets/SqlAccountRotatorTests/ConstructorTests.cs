using System;
using System.Collections.Generic;
using System.Linq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class ConstructorTests : BaseTest
    {
        public enum NotEqualParameters
        {
            PrimaryUsernameSecretName,
            ServerUrl,
            DatabaseName
        }

        public static IEnumerable<object[]> ConstructorFailsIfNotEqualData
        {
            get
            {
                var notEqualParametersSubsets = TestUtils.GetSubsetsOf(Enum.GetValues(typeof(NotEqualParameters)).Cast<NotEqualParameters>());

                var possibleRankTypeTuples = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                                              from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                                              select
                                              Tuple.Create(rank, type)).ToList();

                var possibleTupleSubsets = TestUtils.GetSubsetsOf(possibleRankTypeTuples);

                return notEqualParametersSubsets.SelectMany(
                    notEqualSubset => possibleTupleSubsets.Select(tupleSubset => new object[] { notEqualSubset, tupleSubset }).ToArray()).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(ConstructorFailsIfNotEqualData))]
        public void ConstructorFailsIfNotEqual(IEnumerable<NotEqualParameters> notEqualParameters,
            IEnumerable<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType>> notEqualAccounts)
        {
            // Arrange
            var allAccountsNotEqual = true;
            var sqlAccountSecrets = new List<SqlAccountSecret>();

            // If an account is contained in notEqualAccounts, it will have the parameters specified by notEqualParameters not equal to the rest of the secrets.
            foreach (SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank)))
            {
                foreach (SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType)))
                {
                    string primaryUsernameSecretName = null, serverUrl = null, databaseName = null;
                    if (notEqualAccounts.Contains(Tuple.Create(rank, type)))
                    {
                        if (notEqualParameters.Contains(NotEqualParameters.PrimaryUsernameSecretName))
                        {
                            primaryUsernameSecretName = "notarealprimaryusernamesecretname";
                        }
                        if (notEqualParameters.Contains(NotEqualParameters.ServerUrl))
                        {
                            serverUrl = "notarealserverurl";
                        }
                        if (notEqualParameters.Contains(NotEqualParameters.DatabaseName))
                        {
                            databaseName = "notarealdatabaseurl";
                        }
                    }
                    else
                    {
                        allAccountsNotEqual = false;
                    }

                    sqlAccountSecrets.Add(TestUtils.CreateMockSqlAccountSecret(rank, type,
                        primaryUsernameSecretName: primaryUsernameSecretName, serverUrl: serverUrl,
                        databaseName: databaseName).Object);
                }
            }

            // Act and Assert
            if (allAccountsNotEqual || !notEqualAccounts.Any() || !notEqualParameters.Any())
            {
                // If one of the following conditions are true, the constructor will succeed:
                //  1 - All accounts are not equal.
                //  2 - No accounts are not equal.
                //  3 - No parameters are not equal.
                // Will not throw if success.
                var sqlAccountRotator = new SqlAccountRotator(sqlAccountSecrets);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => new SqlAccountRotator(sqlAccountSecrets));
            }
        }
    }
}
