using Moq;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class TemporarySecretsSettersTests : BaseTest
    {
        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            const string username = "username";
            const string password = "password";

            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: username);
            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: password);

            var sqlAccountRotator =
                TestUtils.CreateMockSqlAccountRotator(new[]
                    {usernameMock.Object, passwordMock.Object});

            await sqlAccountRotator.Object.GenerateTemporarySecretsForAccount(rank);

            usernameMock.Verify(x => x.SetTemporary(It.Is<string>(input => input == username)));
            passwordMock.Verify(x => x.SetTemporary(It.Is<string>(input => input == password)));
        }

        [Theory]
        [MemberData(nameof(BothRanksData))]
        public async void DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            const string username = "username";
            const string password = "password";

            var usernameMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Username, value: username);
            var passwordMock = TestUtils.CreateMockSqlAccountSecret(rank,
                SqlAccountSecret.SqlType.Password, value: password);

            var sqlAccountRotator =
                TestUtils.CreateMockSqlAccountRotator(new[]
                    {usernameMock.Object, passwordMock.Object});

            await sqlAccountRotator.Object.DeleteTemporarySecretsForAccount(rank);

            usernameMock.Verify(x => x.DeleteTemporary());
            passwordMock.Verify(x => x.DeleteTemporary());
        }
    }
}
