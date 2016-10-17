using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using RotateSecrets.SecretRotators;
using RotateSecrets.SecretsToRotate;
using Xunit;

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class IsSecretValidTests : BaseTest
    {
        public static IEnumerable<object[]> IsSecretValidData
        {
            get
            {
                var possibleTaskResults = new[] { SecretRotator.TaskResult.Error, SecretRotator.TaskResult.Recovered, SecretRotator.TaskResult.Valid};

                return possibleTaskResults.SelectMany(
                    taskResult => possibleTaskResults.Select(taskResult2 => new object[] { taskResult, taskResult2 }).ToArray()).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(IsSecretValidData))]
        public async Task IsSecretValid(SecretRotator.TaskResult primaryResult, SecretRotator.TaskResult secondaryResult)
        {
            var sqlAccountRotatorMock = TestUtils.CreateMockSqlAccountRotator();
            sqlAccountRotatorMock.Setup(x => x.IsAccountValid(It.IsAny<SqlAccountSecret.Rank>()))
                .Returns<SqlAccountSecret.Rank>((rank) => Task.FromResult(rank == SqlAccountSecret.Rank.Primary ? primaryResult : secondaryResult));

            // Return TaskResult.Error if either accounts errored.
            // Return the result of the primary otherwise.
            var expected = primaryResult == SecretRotator.TaskResult.Error ||
                           secondaryResult == SecretRotator.TaskResult.Error
                ? SecretRotator.TaskResult.Error
                : primaryResult;

            Assert.Equal(expected, await sqlAccountRotatorMock.Object.IsSecretValid());
        }
    }
}
