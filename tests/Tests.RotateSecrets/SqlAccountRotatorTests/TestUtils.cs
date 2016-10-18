using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public static class TestUtils
    {
        public const string ServerUrl = "serverurl.database.windows.net";
        public const string DatabaseName = "DatabaseName";

        public const string PrimaryUsername = "primaryUsername";
        public const string PrimaryPassword = "primaryPassword";
        public const string SecondaryUsername = "secondaryUsername";
        public const string SecondaryPassword = "secondaryPassword";
        public static string GetMockSecretName(SqlAccountSecret.Rank rank, SqlAccountSecret.SqlType type)
        {
            return Enum.GetName(typeof(SqlAccountSecret.Rank), rank) +
                   Enum.GetName(typeof(SqlAccountSecret.SqlType), type);
        }

        public static Mock<SqlAccountSecret> CreateMockSqlAccountSecret(SqlAccountSecret.Rank rank, SqlAccountSecret.SqlType type,
            string name = null, string value = null, string primaryUsernameSecretName = null, string serverUrl = ServerUrl, string databaseName = DatabaseName, bool? isOutdated = null)
        {
            var secretMock = new Mock<SqlAccountSecret>();

            secretMock.Setup(x => x.CurrentRank).Returns(rank);
            secretMock.Setup(x => x.Type).Returns(type);

            secretMock.Setup(x => x.Name).Returns(name ?? GetMockSecretName(rank, type));

            if (rank == SqlAccountSecret.Rank.Primary && type == SqlAccountSecret.SqlType.Username)
            {
                secretMock.Setup(x => x.PrimaryUsernameSecretName).Returns(name);
            }

            if (value != null)
            {
                secretMock.Setup(x => x.Value).Returns(value);
            }

            var primaryUsernameNameSecretNameToUse = primaryUsernameSecretName ?? GetMockSecretName(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username);
            secretMock.Setup(x => x.PrimaryUsernameSecretName).Returns(primaryUsernameNameSecretNameToUse);

            secretMock.Setup(x => x.ServerUrl).Returns(serverUrl);
            secretMock.Setup(x => x.DatabaseName).Returns(databaseName);

            if (isOutdated.HasValue)
            {
                secretMock.Setup(x => x.IsOutdated()).Returns(isOutdated.Value);
            }

            return secretMock;
        }

        public static IEnumerable<Mock<SqlAccountSecret>> CreateMockSqlAccountSecrets()
        {
            return new[]
            {
                CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username, value: PrimaryUsername),
                CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password, value: PrimaryPassword),
                CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username, value: SecondaryUsername),
                CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password, value: SecondaryPassword)
            };
        }

        public static Mock<SqlAccountRotator> CreateMockSqlAccountRotator(
            IEnumerable<Mock<SqlAccountSecret>> secretMocks)
        {
            return CreateMockSqlAccountRotator(secretMocks.Select(mock => mock.Object));
        }

        public static Mock<SqlAccountRotator> CreateMockSqlAccountRotator(IEnumerable<SqlAccountSecret> secrets)
        {
            return CreateMockSqlAccountRotator(
                GetSecret(secrets, SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username),
                GetSecret(secrets, SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password),
                GetSecret(secrets, SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username),
                GetSecret(secrets, SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password));
        }

        public static Mock<SqlAccountRotator> CreateMockSqlAccountRotator(SqlAccountSecret primaryUsername = null,
            SqlAccountSecret primaryPassword = null, SqlAccountSecret secondaryUsername = null,
            SqlAccountSecret secondaryPassword = null)
        {
            var sqlAccountRotatorMock = new Mock<SqlAccountRotator> { CallBase = true };

            sqlAccountRotatorMock.Setup(x => x.ServerUrl).Returns(ServerUrl);
            sqlAccountRotatorMock.Setup(x => x.DatabaseName).Returns(DatabaseName);

            if (primaryUsername != null)
            {
                sqlAccountRotatorMock.Setup(x => x.PrimaryUsernameSecret).Returns(primaryUsername);
            }

            if (primaryPassword != null)
            {
                sqlAccountRotatorMock.Setup(x => x.PrimaryPasswordSecret).Returns(primaryPassword);
            }

            if (secondaryUsername != null)
            {
                sqlAccountRotatorMock.Setup(x => x.SecondaryUsernameSecret).Returns(secondaryUsername);
            }

            if (secondaryPassword != null)
            {
                sqlAccountRotatorMock.Setup(x => x.SecondaryPasswordSecret).Returns(secondaryPassword);
            }

            return sqlAccountRotatorMock;
        }

        public static SqlAccountSecret GetSecret(IEnumerable<SqlAccountSecret> secrets, SqlAccountSecret.Rank rank,
            SqlAccountSecret.SqlType type)
        {
            try
            {
                return SqlAccountRotator.GetSecret(secrets, rank, type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Mock<SqlAccountSecret> GetSecretMock(this IEnumerable<Mock<SqlAccountSecret>> secretMocks, SqlAccountSecret.Rank rank,
            SqlAccountSecret.SqlType type)
        {
            return secretMocks.Single(secret => secret.Object.CurrentRank == rank && secret.Object.Type == type);
        }

        /// <summary>
        /// Recursively returns all possible subsets by recursing through each element and deciding to either include or not include.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Source from which subsets are constructed.</param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> GetSubsetsOf<T>(IEnumerable<T> source)
        {
            if (!source.Any())
            {
                return Enumerable.Repeat(Enumerable.Empty<T>(), 1);
            }

            var element = source.Take(1);

            var subsetsWithoutElement = GetSubsetsOf(source.Skip(1));
            var subsetsWithElement = subsetsWithoutElement.Select(subset => element.Concat(subset));

            return subsetsWithElement.Concat(subsetsWithoutElement);
        }

        public static Func<SqlConnectionStringBuilder, Task> CheckConnectionStringMatching(string username, string password)
        {
            return CheckConnectionStringMatching(new[] {Tuple.Create(username, password)});
        }

        public static Func<SqlConnectionStringBuilder, Task> CheckConnectionStringMatching(Tuple<string, string>[] logins)
        {
            return (connectionStringBuilder) =>
            {
                Assert.Equal(ServerUrl, connectionStringBuilder.DataSource);
                Assert.Equal("master", connectionStringBuilder.InitialCatalog);
                Assert.Contains(connectionStringBuilder.UserID, logins.Select(login => login.Item1));
                Assert.Contains(connectionStringBuilder.Password, logins.Select(login => login.Item2));

                return Task.FromResult(false);
            };
        }

        public static Func<SqlConnectionStringBuilder, Task> CheckConnectionStringMatching()
        {
            return CheckConnectionStringMatching(new[]
            {
                Tuple.Create(PrimaryUsername, PrimaryPassword),
                Tuple.Create(SecondaryUsername, SecondaryPassword)
            });
        }
    }
}
