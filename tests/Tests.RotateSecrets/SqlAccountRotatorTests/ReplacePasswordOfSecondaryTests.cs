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
    public class ReplacePasswordOfSecondaryTests : RotateSecretsBaseTests
    {
        [Fact]
        public async void ReplacePasswordOfSecondaryFailsIfPrimaryGiven()
        {
            const string primaryUsername = "primaryUsername";
            const string primaryPassword = "primaryPassword";
            const string secondaryUsername = "secondaryUsername";
            const string secondaryPassword = "secondaryPassword";

            var primaryUsernameMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Username, value: primaryUsername);
            var primaryPasswordMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Password, value: primaryPassword);

            var secondaryUsernameMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username, value: secondaryUsername);
            var secondaryPasswordMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password, value: secondaryPassword);

            var sqlAccountRotator = TestUtils.CreateMockSqlAccountRotator(primaryUsernameMock.Object, primaryPasswordMock.Object,
                secondaryUsernameMock.Object, secondaryPasswordMock.Object);

            var primaryConnectionStringBuilder = sqlAccountRotator.Object.BuildConnectionString(primaryUsernameMock.Object,
                primaryPasswordMock.Object);

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await sqlAccountRotator.Object.ReplacePasswordOfSecondary(primaryConnectionStringBuilder));
        }

        /// <summary>
        /// This test verifies that ReplacePasswordOfSecondary behaves as follows:
        ///     1 - The password stored on KeyVault is changed before the SQL server's password is changed.
        ///     2 - AlterSqlPassword is called with the same password that KeyVault is newly changed to store.
        ///     3 - The new password satifies requirements (length, nonalphanumeric characters...).
        ///     4 - The correct credentials are used in every SQL connection.
        /// </summary>
        [Fact]
        public async void ReplacePasswordOfSecondaryChangesPasswordCorrectly()
        {
            // Arrange
            const string secondaryUsername = "secondaryUsername";
            const string secondaryPassword = "secondaryPassword";

            string newPassword = null;

            var primaryUsernameMock = new Mock<SqlAccountSecret>();
            var primaryPasswordMock = new Mock<SqlAccountSecret>();

            var secondaryUsernameMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username, value: secondaryUsername);
            var secondaryPasswordMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password, value: secondaryPassword);

            var sqlAccountRotator = TestUtils.CreateMockSqlAccountRotator(primaryUsernameMock.Object, primaryPasswordMock.Object,
                secondaryUsernameMock.Object, secondaryPasswordMock.Object);

            secondaryPasswordMock.Setup(SetSqlSecretAnyExpression).Returns<string>(input =>
            {
                // We cannot alter the password on the SQL server before we have changed the secret in KeyVault.
                sqlAccountRotator.Verify(AlterPasswordOfSecondary, Times.Never);

                // Change the value returned by the mock.
                secondaryPasswordMock.Setup(x => x.Value).Returns(input);

                return Task.FromResult(false);
            });

            sqlAccountRotator.Setup(TestConnection)
                // Verify the connection is the same as the secondary account.
                .Returns(TestUtils.CheckConnectionStringMatching(secondaryUsername, secondaryPassword));

            sqlAccountRotator.Setup(AlterPasswordOfSecondary)
                .Returns<SqlConnectionStringBuilder, string>(
                    (connString, newPass) =>
                    {
                        newPassword = newPass;

                        // Verify that the password has been changed in KeyVault to the newPassword before the password was changed on the SQL server.
                        secondaryPasswordMock.Verify(x => x.Set(It.Is<string>(input => input == newPassword)));
                        Assert.Equal(newPassword, secondaryPasswordMock.Object.Value);

                        // Verify that we are still using the right credentials.
                        return TestUtils.CheckConnectionStringMatching(secondaryUsername, secondaryPassword)(connString);
                    });

            var secondaryConnectionStringBuilder = sqlAccountRotator.Object.BuildConnectionString(secondaryUsernameMock.Object,
                secondaryPasswordMock.Object);

            // Act
            await sqlAccountRotator.Object.ReplacePasswordOfSecondary(secondaryConnectionStringBuilder);

            // Assert
            // Verify that the password has been altered on the SQL server.
            sqlAccountRotator.Verify(AlterPasswordOfSecondary);

            // The password must be the same length and contain at least as many nonalphanumeric characters as specified.
            Assert.NotNull(newPassword);
            Assert.Equal(SqlAccountRotator.PasswordLength, newPassword.Length);
            Assert.True(SqlAccountRotator.PasswordNumberOfNonAlphanumericCharacters <= newPassword.Count(c => !char.IsLetterOrDigit(c)));

            // The secrets store the correct values.
            Assert.Equal(secondaryUsername, secondaryUsernameMock.Object.Value);
            Assert.Equal(newPassword, secondaryPasswordMock.Object.Value);
        }

        /// <summary>
        /// This test verifies that ReplacePasswordOfSecondary behaves as follows:
        ///     1 - Temporary secrets are generated before changing the value in KeyVault or the login on the SQL server.
        ///     2 - Temporary secrets are deleted after changing the value in KeyVault and the login on the SQL server.
        /// </summary>
        [Fact]
        public async void ReplacePasswordOfSecondaryGeneratesAndDeletesTemporary()
        {
            // Arrange
            const string secondaryUsername = "secondaryUsername";
            const string secondaryPassword = "secondaryPassword";

            var primaryUsernameMock = new Mock<SqlAccountSecret>();
            var primaryPasswordMock = new Mock<SqlAccountSecret>();

            var secondaryUsernameMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username, value: secondaryUsername);
            var secondaryPasswordMock = TestUtils.CreateMockSqlAccountSecret(SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password, value: secondaryPassword);

            var sqlAccountRotator = TestUtils.CreateMockSqlAccountRotator(primaryUsernameMock.Object, primaryPasswordMock.Object,
                secondaryUsernameMock.Object, secondaryPasswordMock.Object);

            secondaryPasswordMock.Setup(SetSqlSecretAnyExpression).Returns<string>(input =>
            {
                // Verify that we have generated temporary secrets before changing the secret.
                sqlAccountRotator.Verify(GenerateTempSecretsForSecondary);

                return Task.FromResult(false);
            });

            sqlAccountRotator.Setup(TestConnection).Returns(Task.FromResult(false));

            sqlAccountRotator.Setup(GenerateTempSecretsForSecondary)
                .Returns<SqlAccountSecret.Rank>(
                    (rank) =>
                    {
                        // Verify that we tested the connection first before we saved it to the temporary.
                        sqlAccountRotator.Verify(TestConnection);

                        // Verify that we have not yet changed the password.
                        secondaryPasswordMock.Verify(SetSqlSecretAnyExpression, Times.Never);
                        sqlAccountRotator.Verify(AlterPasswordOfSecondary, Times.Never);

                        Assert.Equal(secondaryUsername, secondaryUsernameMock.Object.Value);
                        Assert.Equal(secondaryPassword, secondaryPasswordMock.Object.Value);

                        return Task.FromResult(false);
                    });

            sqlAccountRotator.Setup(AlterPasswordOfSecondary)
                .Returns<SqlConnectionStringBuilder, string>(
                    (connString, newPass) =>
                    {
                        // We must have generated the temporary secrets before changing the password!
                        sqlAccountRotator.Verify(GenerateTempSecretsForSecondary);
                        sqlAccountRotator.Verify(DeleteTempSecretsOfSecondary, Times.Never);

                        return Task.FromResult(false);
                    });

            sqlAccountRotator.Setup(DeleteTempSecretsOfSecondary).Returns<SqlAccountSecret.Rank>(
                rank =>
                {
                    // Cannot delete temporary secrets if they have not been generated!
                    sqlAccountRotator.Verify(GenerateTempSecretsForSecondary);

                    // We must have altered the password successfully before we delete the temporary secrets!
                    sqlAccountRotator.Verify(AlterPasswordOfSecondary);

                    return Task.FromResult(false);
                });

            var secondaryConnectionStringBuilder = sqlAccountRotator.Object.BuildConnectionString(secondaryUsernameMock.Object,
                secondaryPasswordMock.Object);

            // Act
            await sqlAccountRotator.Object.ReplacePasswordOfSecondary(secondaryConnectionStringBuilder);

            // Assert
            // Verify we have both generated and deleted the temporary secrets.
            sqlAccountRotator.Verify(GenerateTempSecretsForSecondary);
            sqlAccountRotator.Verify(DeleteTempSecretsOfSecondary);
        }
    }
}
