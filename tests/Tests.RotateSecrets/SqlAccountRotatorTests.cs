using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs;
using NuGet.Services.Logging;
using Xunit;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit.Extensions;

namespace Tests.RotateSecrets
{
    public class SqlAccountRotatorTests
    {
        private const string ServerUrl = "serverurl.database.windows.net";
        private const string DatabaseName = "DatabaseName";

        public SqlAccountRotatorTests()
        {
            if (JobRunner.ServiceContainer.GetService<ILoggerFactory>() != null) return;

            // Add an ILoggerFactory to DI to emulate Job.
            var loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
            JobRunner.ServiceContainer.AddService(loggerFactory);
        }

        #region Mock factory methods
        private static Mock<SqlAccountRotator> CreateMockSqlAccountRotator(IEnumerable<SqlAccountSecret> secrets)
        {
            return CreateMockSqlAccountRotator(
                SqlAccountRotator.GetSecret(secrets, SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username),
                SqlAccountRotator.GetSecret(secrets, SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password),
                SqlAccountRotator.GetSecret(secrets, SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username),
                SqlAccountRotator.GetSecret(secrets, SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password));
        }

        private static Mock<SqlAccountRotator> CreateMockSqlAccountRotator(SqlAccountSecret primaryUsername = null,
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

        private static string GetMockSecretName(SqlAccountSecret.Rank rank, SqlAccountSecret.SqlType type)
        {
            return Enum.GetName(typeof(SqlAccountSecret.Rank), rank) +
                   Enum.GetName(typeof(SqlAccountSecret.SqlType), type);
        }

        private static Mock<SqlAccountSecret> CreateMockSqlAccountSecret(SqlAccountSecret.Rank rank, SqlAccountSecret.SqlType type,
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
            if (primaryUsernameSecretName != null)
            {
                secretMock.Setup(x => x.PrimaryUsernameSecretName).Returns(primaryUsernameNameSecretNameToUse);
            }

            secretMock.Setup(x => x.ServerUrl).Returns(serverUrl);
            secretMock.Setup(x => x.DatabaseName).Returns(databaseName);

            if (isOutdated.HasValue)
            {
                secretMock.Setup(x => x.IsOutdated()).Returns(isOutdated.Value);
            }

            return secretMock;
        }
        #endregion

        #region Utility
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

        private static Func<SqlConnectionStringBuilder, Task> CheckConnectionStringMatching(string username, string password)
        {
#pragma warning disable 1998
            // The method we are spoofing must be async, so it's ok that this method can be made synchronous.
            return async (connectionStringBuilder) =>
            {
                Assert.Equal(ServerUrl, connectionStringBuilder.DataSource);
                Assert.Equal(DatabaseName, connectionStringBuilder.InitialCatalog);
                Assert.Equal(username, connectionStringBuilder.UserID);
                Assert.Equal(password, connectionStringBuilder.Password);
            };
#pragma warning restore 1998
        }
        #endregion

        #region Constructor tests
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
                var notEqualParametersSubsets = GetSubsetsOf(Enum.GetValues(typeof(NotEqualParameters)).Cast<NotEqualParameters>());

                var possibleRankTypeTuples = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                                              from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                                              select
                                              Tuple.Create(rank, type)).ToList();

                var possibleTupleSubsets = GetSubsetsOf(possibleRankTypeTuples);

                return notEqualParametersSubsets.SelectMany(
                    notEqualSubset => possibleTupleSubsets.Select(tupleSubset => new object[] { notEqualSubset, tupleSubset }).ToArray()).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(ConstructorFailsIfNotEqualData))]
        public void ConstructorFailsIfNotEqual(IEnumerable<NotEqualParameters> notEqualParameters,
            IEnumerable<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType>> notEqualAccounts)
        {
            var allAccountsNotEqual = true;
            var sqlAccountSecrets = new List<SqlAccountSecret>();
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

                    sqlAccountSecrets.Add(CreateMockSqlAccountSecret(rank, type,
                        primaryUsernameSecretName: primaryUsernameSecretName, serverUrl: serverUrl,
                        databaseName: databaseName).Object);
                }
            }
            
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
        #endregion

        #region IsAccountValid tests
        [Theory]
        [InlineData(SqlAccountSecret.Rank.Primary)]
        [InlineData(SqlAccountSecret.Rank.Secondary)]
        public async Task IsAccountValidSuccess(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";

            var usernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: username);
            var passwordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: password);

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ? 
                CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) : 
                CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(CheckConnectionStringMatching(username, password));

            // Act-Assert
            Assert.Equal(SecretRotator.TaskResult.Valid, await sqlAccountRotatorMock.Object.IsAccountValid(rank));
        }

        [Theory]
        [InlineData(SqlAccountSecret.Rank.Primary)]
        [InlineData(SqlAccountSecret.Rank.Secondary)]
        public async Task IsAccountValidRecovered(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";

            var usernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: invalidUsername);

            var tempUsernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: username);
            usernameMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempUsernameMock.Object));

            var passwordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: invalidPassword);

            var tempPasswordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: password);
            passwordMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempPasswordMock.Object));

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(CheckConnectionStringMatching(username, password));

            // Act-Assert
            Assert.Equal(SecretRotator.TaskResult.Recovered, await sqlAccountRotatorMock.Object.IsAccountValid(rank));
        }

        [Theory]
        [InlineData(SqlAccountSecret.Rank.Primary)]
        [InlineData(SqlAccountSecret.Rank.Secondary)]
        public async Task IsAccountValidErrorNoTemporary(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";

            var usernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidUsername);
            usernameMock.Setup(x => x.GetTemporary()).Throws<Exception>();

            var passwordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidPassword);
            passwordMock.Setup(x => x.GetTemporary()).Throws<Exception>();

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(CheckConnectionStringMatching(username, password));

            // Act-Assert
            Assert.Equal(SecretRotator.TaskResult.Error, await sqlAccountRotatorMock.Object.IsAccountValid(rank));
        }

        [Theory]
        [InlineData(SqlAccountSecret.Rank.Primary)]
        [InlineData(SqlAccountSecret.Rank.Secondary)]
        public async Task IsAccountValidErrorWithTemporary(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";
            const string invalidUsername2 = "invalidUsername2";
            const string invalidPassword2 = "invalidPassword2";

            var usernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidUsername);

            var tempUsernameMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidUsername2);
            usernameMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempUsernameMock.Object));

            var passwordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidPassword);

            var tempPasswordMock = CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidPassword2);
            passwordMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempPasswordMock.Object));

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(CheckConnectionStringMatching(username, password));

            // Act-Assert
            Assert.Equal(SecretRotator.TaskResult.Error, await sqlAccountRotatorMock.Object.IsAccountValid(rank));
        }
        #endregion

        #region IsSecretValid tests
        [Theory]

        // Error if either account errors.
        [InlineData(SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Error)]
        [InlineData(SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Error)]
        [InlineData(SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Error)]
        [InlineData(SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Error)]
        [InlineData(SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Error)]

        // If both accounts are valid, the secret is valid.
        [InlineData(SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Valid)]

        // If both accounts are recovered, the secret cannot be rotated and is recovered.
        [InlineData(SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Recovered)]

        // If the primary is valid but the secondary is recovered, the secret can be rotated and is valid.
        [InlineData(SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Valid)]

        // If the primary has been recovered but the secondary is valid, the secret cannot be rotated and is recovered.
        [InlineData(SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Valid, SecretRotator.TaskResult.Recovered)]

        public async Task IsSecretValid(SecretRotator.TaskResult primaryResult, SecretRotator.TaskResult secondaryResult,
            SecretRotator.TaskResult expected)
        {
            var sqlAccountRotatorMock = CreateMockSqlAccountRotator();
            sqlAccountRotatorMock.Setup(x => x.IsAccountValid(It.IsAny<SqlAccountSecret.Rank>()))
                .Returns<SqlAccountSecret.Rank>((rank) => Task.FromResult(rank == SqlAccountSecret.Rank.Primary ? primaryResult : secondaryResult));

            Assert.Equal(expected, await sqlAccountRotatorMock.Object.IsSecretValid());
        }
        #endregion

        #region IsSecretOutdated tests
        public static IEnumerable<object[]> SecretOnlyOutdatedIfPrimarySecretsOutdatedData
        {
            get
            {
                var possibleRankTypeTuples = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                                              from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                                              select
                                              Tuple.Create(rank, type)).ToList();

                return GetSubsetsOf(possibleRankTypeTuples).Select(subset => new [] { subset });
            }
        }

        [Theory]
        [MemberData(nameof(SecretOnlyOutdatedIfPrimarySecretsOutdatedData))]
        public void SecretOnlyOutdatedIfPrimarySecretsOutdated(IEnumerable<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType>> outdatedSecrets)
        {
            var sqlAccountSecrets = (from SqlAccountSecret.Rank rank in Enum.GetValues(typeof(SqlAccountSecret.Rank))
                from SqlAccountSecret.SqlType type in Enum.GetValues(typeof(SqlAccountSecret.SqlType))
                select
                CreateMockSqlAccountSecret(rank, type, isOutdated: outdatedSecrets.Contains(Tuple.Create(rank, type))).Object).ToList();

            var primaryAccountIsOutdated =
                outdatedSecrets.Contains(Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username)) &&
                outdatedSecrets.Contains(Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password));

            Assert.Equal(
                primaryAccountIsOutdated ? SecretRotator.TaskResult.Outdated : SecretRotator.TaskResult.Recent,
                CreateMockSqlAccountRotator(sqlAccountSecrets).Object.IsSecretOutdated());
        }
        #endregion
    }
}
