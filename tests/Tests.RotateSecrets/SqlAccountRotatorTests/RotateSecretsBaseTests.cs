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

namespace Tests.RotateSecrets.SqlAccountRotatorTests
{
    public class RotateSecretsBaseTests : BaseTest
    {
        public static readonly Expression<Func<SqlAccountSecret, Task>> SetSqlSecretAnyExpression = x => x.Set(It.IsAny<string>());

        public static readonly Expression<Func<SqlAccountRotator, Task>> TestConnection = x => x.TestSqlConnection(It.IsAny<SqlConnectionStringBuilder>());

        public static readonly Expression<Func<SqlAccountRotator, Task>> DeleteTempSecretsOfSecondary =
            x =>
                x.DeleteTemporarySecretsForAccount(
                    It.Is<SqlAccountSecret.Rank>(rank => rank == SqlAccountSecret.Rank.Secondary));

        public static readonly Expression<Func<SqlAccountRotator, Task>> GenerateTempSecretsForSecondary = x =>
            x.GenerateTemporarySecretsForAccount(
                It.Is<SqlAccountSecret.Rank>(rank => rank == SqlAccountSecret.Rank.Secondary));

        public static readonly Expression<Func<SqlAccountRotator, Task>> AlterPasswordOfSecondary =
            x => x.AlterSqlPassword(It.IsAny<SqlConnectionStringBuilder>(), It.IsAny<string>());
    }
}
