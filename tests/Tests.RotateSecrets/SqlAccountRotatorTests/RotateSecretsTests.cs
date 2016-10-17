using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class RotateSecretsTests : RotateSecretsBaseTests
    {
        private const string newPassword = "newPassword";

        private void SetupReplacePasswordOfSecondary(Mock<SqlAccountRotator> sqlAccountRotatorMock, IEnumerable<Mock<SqlAccountSecret>> secretMocks)
        {
            sqlAccountRotatorMock.Setup(x => x.ReplacePasswordOfSecondary(It.IsAny<SqlConnectionStringBuilder>()))
                .Returns<SqlConnectionStringBuilder>(
                    async connString =>
                    {
                        secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password)
                            .Setup(x => x.Value)
                            .Returns(newPassword);
                        await TestUtils.CheckConnectionStringMatching(TestUtils.SecondaryUsername, TestUtils.SecondaryPassword)(connString);
                    });
        }

        private void SetupSwapSecretsForAccounts(Mock<SqlAccountRotator> sqlAccountRotatorMock, IEnumerable<Mock<SqlAccountSecret>> secretMocks)
        {
            var primaryUsernameMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Username);
            var primaryPasswordMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Password);
            var secondaryUsernameMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username);
            var secondaryPasswordMock = secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password);

            sqlAccountRotatorMock.Setup(x => x.SwapSecretsForAccounts())
                .Returns(() =>
                {
                    var oldPrimaryUsername = primaryUsernameMock.Object.Value;
                    var oldPrimaryPassword = primaryPasswordMock.Object.Value;

                    primaryUsernameMock.Setup(x => x.Value).Returns(secondaryUsernameMock.Object.Value);
                    primaryPasswordMock.Setup(x => x.Value).Returns(secondaryPasswordMock.Object.Value);

                    secondaryUsernameMock.Setup(x => x.Value).Returns(oldPrimaryUsername);
                    secondaryPasswordMock.Setup(x => x.Value).Returns(oldPrimaryPassword);

                    return Task.FromResult(false);
                });
        }

        [Fact]
        public async void RotateSecretsSuccess()
        {
            // Arrange
            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();

            var sqlAccountRotatorMock = TestUtils.CreateMockSqlAccountRotator(secretMocks.Select(secret => secret.Object));
            SetupReplacePasswordOfSecondary(sqlAccountRotatorMock, secretMocks);
            SetupSwapSecretsForAccounts(sqlAccountRotatorMock, secretMocks);

            // Act
            var result = await sqlAccountRotatorMock.Object.RotateSecret();

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Success, result);

            Assert.Equal(TestUtils.SecondaryUsername,
                secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username).Object.Value);
            Assert.Equal(newPassword,
                secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password).Object.Value);

            Assert.Equal(TestUtils.PrimaryUsername,
                secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username)
                    .Object.Value);
            Assert.Equal(TestUtils.PrimaryPassword,
                secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password)
                    .Object.Value);
        }

        public enum RotateSecretsErrorParameter
        {
            FailOnReplacePasswordOfSecondary,
            FailOnSwapSecretsForAccounts
        }

        [Theory]
        [InlineData(RotateSecretsErrorParameter.FailOnReplacePasswordOfSecondary)]
        [InlineData(RotateSecretsErrorParameter.FailOnSwapSecretsForAccounts)]
        public async void RotateSecretsError(RotateSecretsErrorParameter parameter)
        {
            // Arrange
            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();
            var sqlAccountRotatorMock = TestUtils.CreateMockSqlAccountRotator(secretMocks);
            
            if (parameter == RotateSecretsErrorParameter.FailOnReplacePasswordOfSecondary)
            {
                sqlAccountRotatorMock.Setup(x => x.ReplacePasswordOfSecondary(It.IsAny<SqlConnectionStringBuilder>()))
                    .Throws<ArgumentException>();
            }
            else
            {
                SetupReplacePasswordOfSecondary(sqlAccountRotatorMock, secretMocks);
            }

            if (parameter == RotateSecretsErrorParameter.FailOnSwapSecretsForAccounts)
            {
                sqlAccountRotatorMock.Setup(x => x.SwapSecretsForAccounts()).Throws<ArgumentException>();
            }
            else
            {
                SetupSwapSecretsForAccounts(sqlAccountRotatorMock, secretMocks);
            }

            // Act
            var result = await sqlAccountRotatorMock.Object.RotateSecret();

            // Assert
            Assert.Equal(SecretRotator.TaskResult.Error, result);
        }
    }
}
