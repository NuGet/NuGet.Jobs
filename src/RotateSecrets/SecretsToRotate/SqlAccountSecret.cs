using System;
using Microsoft.Azure.KeyVault;

namespace RotateSecrets.SecretsToRotate
{
    public class SqlAccountSecret : SecretToRotate
    {
        public virtual SqlType Type { get; }
        public enum SqlType
        {
            Username,
            Password
        }

        public virtual Rank CurrentRank { get; }
        public enum Rank
        {
            Primary,
            Secondary
        }

        /// <summary>
        /// The name of the primary username secret associated with this account.
        /// </summary>
        public virtual string PrimaryUsernameSecretName { get; }

        public virtual string ServerUrl { get; }
        public virtual string DatabaseName { get; }

        /// <summary>
        /// For testing.
        /// </summary>
        public SqlAccountSecret()
            : base(null)
        {
        }

        public SqlAccountSecret(Secret secret)
            : base(secret)
        {
            PrimaryUsernameSecretName = secret.Tags[Tags.SqlPrimaryUserSecretName];
            ServerUrl = secret.Tags[Tags.SqlServerUrl];
            DatabaseName = secret.Tags[Tags.SqlDatabaseName];

            var sqlType = secret.Tags[Tags.SqlType];
            switch (sqlType)
            {
                case Tags.SqlTypeUser:
                    Type = SqlType.Username;
                    break;
                case Tags.SqlTypePassword:
                    Type = SqlType.Password;
                    break;
                default:
                    throw new ArgumentException($"{secret.SecretIdentifier.Name} has a sql type {sqlType} that is unsupported!");
            }

            var rank = secret.Tags[Tags.SqlRank];
            switch (rank)
            {
                case Tags.SqlRankPrimary:
                    CurrentRank = Rank.Primary;
                    break;
                case Tags.SqlRankSecondary:
                    CurrentRank = Rank.Secondary;
                    break;
                default:
                    throw new ArgumentException($"{secret.SecretIdentifier.Name} has a rank {rank} that is unsupported!");
            }
        }
    }
}
