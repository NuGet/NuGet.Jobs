using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Moq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class IsAccountValidTests : BaseTest
    {
        private static readonly Expression<Func<SqlAccountSecret, Task>> _deleteTempExpression = x => x.DeleteTemporary();

        /// <summary>
        /// If the Username and Password secrets store valid logins, IsAccountValid should return TaskResult.Success.
        /// </summary>
        /// <param name="rank">Rank to test behavior for.</param>
        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void IsAccountValidSuccess(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";

            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: username);
            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: password);

            var otherRank = rank == SqlAccountSecret.Rank.Primary
                ? SqlAccountSecret.Rank.Secondary
                : SqlAccountSecret.Rank.Primary;
            var otherUsernameMock = TestUtils.CreateMockSqlAccountSecret(otherRank, SqlAccountSecret.SqlType.Username);
            var otherPasswordMock = TestUtils.CreateMockSqlAccountSecret(otherRank, SqlAccountSecret.SqlType.Password);

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary
                ? TestUtils.CreateMockSqlAccountRotator(usernameMock.Object, passwordMock.Object,
                    otherUsernameMock.Object, otherPasswordMock.Object)
                : TestUtils.CreateMockSqlAccountRotator(otherUsernameMock.Object, otherPasswordMock.Object,
                    usernameMock.Object, passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(TestUtils.CheckConnectionStringMatching(username, password));

            // Act
            var result = await sqlAccountRotatorMock.Object.IsAccountValid(rank);

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Valid, result);
        }

        private static void SetupRecoverableTemp(Mock<SqlAccountSecret> secretMock, string tempValue)
        {
            var tempSecretMock = TestUtils.CreateMockSqlAccountSecret(secretMock.Object.CurrentRank,
                SqlAccountSecret.SqlType.Username, value: tempValue);
            secretMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempSecretMock.Object));
            secretMock.Setup(_deleteTempExpression).Returns(() =>
            {
                // Must have set the secret to the valid value before deleting the temporary secret!
                secretMock.Verify(x => x.Set(It.Is<string>(input => input == tempValue)));
                return Task.FromResult(false);
            });
        }

        /// <summary>
        /// If the Username and Password secrets store invalid logins, but the temporary secrets store valid logins,
        /// IsAccountValid should update the secrets with the valid logins, delete the temporary secrets, and return TaskResult.Recovered.
        /// </summary>
        /// <param name="rank">Rank to test behavior for.</param>
        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void IsAccountValidRecoversFromTemporary(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string username = "username";
            const string password = "password";
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";

            Expression<Func<SqlAccountSecret, Task>> deleteTempExpression = x => x.DeleteTemporary();

            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: invalidUsername);
            SetupRecoverableTemp(usernameMock, username);

            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: invalidPassword);
            SetupRecoverableTemp(passwordMock, password);

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                TestUtils.CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                TestUtils.CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(TestUtils.CheckConnectionStringMatching(username, password));

            // Act
            var result = await sqlAccountRotatorMock.Object.IsAccountValid(rank);

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Recovered, result);

            usernameMock.Verify(deleteTempExpression);
            passwordMock.Verify(deleteTempExpression);
        }

        /// <summary>
        /// If the primary and secondary accounts are identical, the secondary should be restored from temporary secrets.
        /// </summary>
        /// <param name="rank">Rank to test behavior for.</param>
        [Fact]
        public async void IsAccountValidRecoverPrimaryAndSecondaryIdentical()
        {
            // Arrange
            Expression<Func<SqlAccountSecret, Task>> deleteTempExpression = x => x.DeleteTemporary();
            Expression<Func<SqlAccountSecret, string>> valueExpression = x => x.Value;

            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();
            
            var secondaryUsernameMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username);
            var secondaryPasswordMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password);

            // Set the value of the secondary account secrets to the same as the primary account.
            secondaryUsernameMock.Setup(valueExpression).Returns(TestUtils.PrimaryUsername);
            secondaryPasswordMock.Setup(valueExpression).Returns(TestUtils.PrimaryPassword);

            // Setup temporary secrets for the secondary account that return the real secondary account.
            SetupRecoverableTemp(secondaryUsernameMock, TestUtils.SecondaryUsername);
            SetupRecoverableTemp(secondaryPasswordMock, TestUtils.SecondaryPassword);

            var sqlAccountRotatorMock = TestUtils.CreateMockSqlAccountRotator(secretMocks);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(TestUtils.CheckConnectionStringMatching());

            // Act
            var result = await sqlAccountRotatorMock.Object.IsAccountValid(SqlAccountSecret.Rank.Secondary);

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Recovered, result);

            secondaryUsernameMock.Verify(deleteTempExpression);
            secondaryPasswordMock.Verify(deleteTempExpression);
        }

        /// <summary>
        /// If the Username and Password secrets store invalid logins and there are no temporary secrets,
        /// IsAccountValid should return TaskResult.Error.
        /// </summary>
        /// <param name="rank">Rank to test behavior for.</param>
        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void IsAccountValidErrorNoTemporary(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";

            // Create an account with invalid username and password credentials.
            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidUsername);
            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidPassword);

            // Make this account have no temporary.
            usernameMock.Setup(x => x.GetTemporary()).Throws<Exception>();
            passwordMock.Setup(x => x.GetTemporary()).Throws<Exception>();

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                TestUtils.CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                TestUtils.CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(TestUtils.CheckConnectionStringMatching());

            // Act
            var result = await sqlAccountRotatorMock.Object.IsAccountValid(rank);

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Error, result);
        }

        /// <summary>
        /// If the Username, Password, and temporary secrets store invalid logins,
        /// IsAccountValid should return TaskResult.Error.
        /// </summary>
        /// <param name="rank">Rank to test behavior for.</param>
        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void IsAccountValidErrorWithTemporary(SqlAccountSecret.Rank rank)
        {
            // Arrange
            const string invalidUsername = "invalidUsername";
            const string invalidPassword = "invalidPassword";
            const string invalidTemporaryUsername = "invalidTemporaryUsername";
            const string invalidTemporaryPassword = "invalidTemporaryPassword";

            // Create an account with invalid username and password credentials.
            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidUsername);

            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidPassword);

            // Give this account temporaries with invalid username and password credentials.
            var tempUsernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, invalidTemporaryUsername);
            usernameMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempUsernameMock.Object));

            var tempPasswordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, invalidTemporaryPassword);
            passwordMock.Setup(x => x.GetTemporary()).Returns(Task.FromResult<SecretToRotate>(tempPasswordMock.Object));

            var sqlAccountRotatorMock = rank == SqlAccountSecret.Rank.Primary ?
                TestUtils.CreateMockSqlAccountRotator(primaryUsername: usernameMock.Object, primaryPassword: passwordMock.Object) :
                TestUtils.CreateMockSqlAccountRotator(secondaryUsername: usernameMock.Object, secondaryPassword: passwordMock.Object);

            sqlAccountRotatorMock.Setup(x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns(TestUtils.CheckConnectionStringMatching());

            // Act
            var result = await sqlAccountRotatorMock.Object.IsAccountValid(rank);

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Error, result);
        }
    }
}
