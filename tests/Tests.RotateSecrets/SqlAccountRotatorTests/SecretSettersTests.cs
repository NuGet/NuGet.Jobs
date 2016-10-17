using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class SecretSettersTests : BaseTest
    {
        /// <summary>
        /// This test verifies that the primary and secondary accounts have their values swapped after a rotation.
        /// </summary>
        [Fact]
        public async void SwapSecretsSwapsValues()
        {
            // Arrange
            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();
            var sqlAccountRotator =
                TestUtils.CreateMockSqlAccountRotator(secretMocks);

            // Act
            await sqlAccountRotator.Object.SwapSecretsForAccounts();

            // Assert
            secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username)
                .Verify(x => x.Set(It.Is<string>(input => input == TestUtils.SecondaryUsername)));
            secretMocks.GetSecretMock(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password)
                .Verify(x => x.Set(It.Is<string>(input => input == TestUtils.SecondaryPassword)));

            secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username)
                .Verify(x => x.Set(It.Is<string>(input => input == TestUtils.PrimaryUsername)));
            secretMocks.GetSecretMock(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password)
                .Verify(x => x.Set(It.Is<string>(input => input == TestUtils.PrimaryPassword)));
        }

        /// <summary>
        /// Sets up methods on a SqlAccountSecret mock that allow creation and deletion of temporary secrets.
        /// </summary>
        /// <param name="sqlAccountSecretMock"></param>
        private static void SetupSetDeleteTempSqlAccountSecretMock(Mock<SqlAccountSecret> sqlAccountSecretMock)
        {
            var initialValue = sqlAccountSecretMock.Object.Value;

            Expression<Func<SqlAccountSecret, Task>> setTempInitialExpression =
                x => x.SetTemporary(It.Is<string>(input => input == initialValue));

            sqlAccountSecretMock.Setup(setTempInitialExpression).Returns(
                () =>
                {
                    // Need to generate temporary secrets before setting the secret itself!
                    sqlAccountSecretMock.Verify(x => x.Set(It.IsAny<string>()), Times.Never());
                    return Task.FromResult(false);
                });

            sqlAccountSecretMock.Setup(x => x.DeleteTemporary()).Returns(() =>
            {
                // Need to generate temporary secrets before deleting them!
                sqlAccountSecretMock.Verify(setTempInitialExpression);
                return Task.FromResult(false);
            });
        }

        /// <summary>
        /// This test verifies that the primary and secondary accounts have their old values saved as temporary secrets before swapping secrets and deleted after.
        /// </summary>
        [Fact]
        public async void SwapSecretsCreatesTemporary()
        {
            // Arrange
            Expression<Func<SqlAccountSecret, Task>> deleteTemporaryExpression = x => x.DeleteTemporary();

            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();
            foreach (var mock in secretMocks)
            {
                SetupSetDeleteTempSqlAccountSecretMock(mock);
            }

            var sqlAccountRotator = TestUtils.CreateMockSqlAccountRotator(secretMocks);

            // Act
            await sqlAccountRotator.Object.SwapSecretsForAccounts();

            // Assert
            foreach (var mock in secretMocks)
            {
                // Verify that every mock had their temporary created and deleted.
                mock.Verify(deleteTemporaryExpression);
            }
        }

        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void ReplacesSecretsWithTemporary(SqlAccountSecret.Rank rankToReplace)
        {
            // Arrange
            Expression<Func<SqlAccountSecret, Task<SecretToRotate>>> getTempExpression = x => x.GetTemporary();

            const string newUsername = "newUsername";
            const string newPassword = "newPassword";

            var secretMocks = TestUtils.CreateMockSqlAccountSecrets();

            // Create temporary secrets with new values to replace the original with.
            var tempUsernameMock = TestUtils.CreateMockSqlAccountSecret(rankToReplace, SqlAccountSecret.SqlType.Username,
                value: newUsername);
            var tempPasswordMock = TestUtils.CreateMockSqlAccountSecret(rankToReplace, SqlAccountSecret.SqlType.Password,
                value: newPassword);

            secretMocks.GetSecretMock(rankToReplace, SqlAccountSecret.SqlType.Username)
                .Setup(getTempExpression)
                .Returns(Task.FromResult<SecretToRotate>(tempUsernameMock.Object));
            secretMocks.GetSecretMock(rankToReplace, SqlAccountSecret.SqlType.Password)
                .Setup(getTempExpression)
                .Returns(Task.FromResult<SecretToRotate>(tempPasswordMock.Object));

            var sqlAccountRotator = TestUtils.CreateMockSqlAccountRotator(secretMocks);

            // Act
            await sqlAccountRotator.Object.ReplaceSecretsForAccountWithTemporary(rankToReplace);

            // Assert
            secretMocks.GetSecretMock(rankToReplace, SqlAccountSecret.SqlType.Username).Verify(x => x.Set(It.Is<string>(input => input == newUsername)));
            secretMocks.GetSecretMock(rankToReplace, SqlAccountSecret.SqlType.Password).Verify(x => x.Set(It.Is<string>(input => input == newPassword)));
        }
    }
}
