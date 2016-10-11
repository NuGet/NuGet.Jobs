using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Security;
using Microsoft.Extensions.Logging;
using RotateSecrets.SecretsToRotate;

namespace RotateSecrets.SecretRotators
{
    /// <summary>
    /// A SecretRotator that rotates the secrets for a SQL account.
    /// Requires four secrets, a primary username, a primary password, a secondary username, and a secondary password.
    /// </summary>
    public class SqlAccountRotator
        : SecretRotator
    {
        /// <summary>
        /// Passwords regenerated during rotation have exactly this length.
        /// </summary>
        private const int PasswordLength = 16;
        /// <summary>
        /// Passwords regenerated during rotator have exactly this many alphanumerical characters.
        /// </summary>
        private const int PasswordNumberOfAlphanumericalCharacters = 4;

        /// <summary>
        /// The url of the server that this account is on.
        /// </summary>
        public virtual string ServerUrl { get; }
        /// <summary>
        /// The name of the database that this account is on.
        /// </summary>
        public virtual string DatabaseName { get; }

        /// <summary>
        /// The secret that represents the primary username of this account.
        /// </summary>
        public virtual SqlAccountSecret PrimaryUsernameSecret { get; }
        /// <summary>
        /// The secret that represents the primary password of this account.
        /// </summary>
        public virtual SqlAccountSecret PrimaryPasswordSecret { get; }

        /// <summary>
        /// The secret that represents the secondary username of this account.
        /// </summary>
        public virtual SqlAccountSecret SecondaryUsernameSecret { get; }
        /// <summary>
        /// The secret that represents the secondary password of this account.
        /// </summary>
        public virtual SqlAccountSecret SecondaryPasswordSecret { get; }

        /// <summary>
        /// For testing.
        /// </summary>
        public SqlAccountRotator()
        {
        }

        public SqlAccountRotator(IList<SqlAccountSecret> sqlAccountSecrets)
        {
            if (sqlAccountSecrets.Count != 4)
            {
                throw new ArgumentException("Must provide exactly 4 secrets!");
            }

            PrimaryUsernameSecret = GetSecret(sqlAccountSecrets, SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Username);
            PrimaryPasswordSecret = GetSecret(sqlAccountSecrets, SqlAccountSecret.Rank.Primary,
                SqlAccountSecret.SqlType.Password);
            SecondaryUsernameSecret = GetSecret(sqlAccountSecrets, SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Username);
            SecondaryPasswordSecret = GetSecret(sqlAccountSecrets, SqlAccountSecret.Rank.Secondary,
                SqlAccountSecret.SqlType.Password);

            if (sqlAccountSecrets.Any(
                secret => secret.PrimaryUsernameSecretName != sqlAccountSecrets[0].PrimaryUsernameSecretName))
            {
                throw new ArgumentException("Not all secrets have the same primary username secret name!");
            }

            if (sqlAccountSecrets.Any(secret => secret.ServerUrl != sqlAccountSecrets[0].ServerUrl))
            {
                throw new ArgumentException("Not all secrets have the same server url!");
            }

            if (sqlAccountSecrets.Any(secret => secret.DatabaseName != sqlAccountSecrets[0].DatabaseName))
            {
                throw new ArgumentException("Not all secrets have the same database name!");
            }

            ServerUrl = sqlAccountSecrets[0].ServerUrl;
            DatabaseName = sqlAccountSecrets[0].DatabaseName;
        }

        public static SqlAccountSecret GetSecret(IEnumerable<SqlAccountSecret> secrets, SqlAccountSecret.Rank rank,
            SqlAccountSecret.SqlType type)
        {
            return secrets.Single(secret => secret.Type == type && secret.CurrentRank == rank);
        }

        private void GetAccountAndLogIndexesFromRank(SqlAccountSecret.Rank rank,
            out SqlAccountSecret username, out SqlAccountSecret password, out int usernameLogIndex,
            out int passwordLogIndex)
        {
            GetAccountFromRank(rank, out username, out password);
            GetLogIndexesFromRank(rank, out usernameLogIndex, out passwordLogIndex);
        }
        
        private void GetAccountFromRank(SqlAccountSecret.Rank rank, 
            out SqlAccountSecret username, out SqlAccountSecret password)
        {
            var isPrimary = rank == SqlAccountSecret.Rank.Primary;
            username = isPrimary ? PrimaryUsernameSecret : SecondaryUsernameSecret;
            password = isPrimary ? PrimaryPasswordSecret : SecondaryPasswordSecret;
        }

        private static void GetLogIndexesFromRank(SqlAccountSecret.Rank rank, 
            out int usernameLogIndex, out int passwordLogIndex)
        {
            var isPrimary = rank == SqlAccountSecret.Rank.Primary;
            usernameLogIndex = isPrimary ? 0 : 2;
            passwordLogIndex = isPrimary ? 1 : 3;
        }

        private const string FormatStringRegEx = "\\{([0-9]*)\\}";

        /// <summary>
        /// Logs to Logger by formatting a message with a set of parameters.
        /// </summary>
        /// <param name="logLevel">LogLevel to log with. Only supports Information, Warning, Error, and Critical.</param>
        /// <param name="message">
        /// A formattable string. Supports the following values:
        /// {0}: PrimaryUsernameSecretName,
        /// {1}: PrimaryPasswordSecretName,
        /// {2}: SecondaryUsernameSecretName,
        /// {3}: SecondaryPasswordSecretName,
        /// {4}: ServerUrl,
        /// {5}: DatabaseName,
        /// {6}: Exception.
        /// </param>
        /// <param name="exception">An exception to log (optional).</param>
        private void LogHelper(LogLevel logLevel, string message, Exception exception = null)
        {
            IList<Tuple<string, Func<object>>> loggerParameters = new List
                <Tuple<string, Func<object>>>
                {
                    Tuple.Create<string, Func<object>>("PrimaryUsernameSecretName",
                        () => PrimaryUsernameSecret.Name),
                    Tuple.Create<string, Func<object>>("PrimaryPasswordSecretName",
                        () => PrimaryPasswordSecret.Name),
                    Tuple.Create<string, Func<object>>("SecondaryUsernameSecretName",
                        () => SecondaryUsernameSecret.Name),
                    Tuple.Create<string, Func<object>>("SecondaryPasswordSecretName",
                        () => SecondaryPasswordSecret.Name),
                    Tuple.Create<string, Func<object>>("ServerUrl",
                        () => ServerUrl),
                    Tuple.Create<string, Func<object>>("DatabaseName",
                        () => DatabaseName),
                    Tuple.Create<string, Func<object>>("Exception", 
                        () => exception)
                };

            // Format the message to convert the indices to parameter names.
            // E.g. {0} becomes {PrimaryUsernameSecretName}
            var logMessage = string.Format(message, loggerParameters.Select(tuple => "{" + tuple.Item1 + "}").ToArray());

            // We only want the params list to contain the parameters we included in our message.
            // Find the indices that are actually used in the log message.
            var indicesInFormatString = new HashSet<int>();
            foreach (Match match in Regex.Matches(message, FormatStringRegEx))
            {
                int index;
                if (int.TryParse(match.Groups[0].Value, out index))
                {
                    indicesInFormatString.Add(index);
                }
            }
            var usedLoggerParameters = indicesInFormatString.Select(index => loggerParameters[index]);
            
            switch (logLevel)
            {
                case LogLevel.Information:
                    Logger.LogInformation(logMessage, usedLoggerParameters.Select(tuple => tuple.Item2()).ToArray());
                    break;
                case LogLevel.Warning:
                    Logger.LogWarning(logMessage, usedLoggerParameters.Select(tuple => tuple.Item2()).ToArray());
                    break;
                case LogLevel.Error:
                    Logger.LogError(logMessage, usedLoggerParameters.Select(tuple => tuple.Item2()).ToArray());
                    break;
                case LogLevel.Critical:
                    Logger.LogCritical(logMessage, usedLoggerParameters.Select(tuple => tuple.Item2()).ToArray());
                    break;
            }
        }

        public override async Task<TaskResult> IsSecretValid()
        {
            LogHelper(LogLevel.Information, "{0}: Validating SQL account with server {4} and database {5}.");

            var isPrimaryValidResult = await IsAccountValid(SqlAccountSecret.Rank.Primary);
            var isSecondaryValidResult = await IsAccountValid(SqlAccountSecret.Rank.Secondary);

            // If either the primary or the secondary account could not be validated, return TaskResult.Error!
            if (isPrimaryValidResult == TaskResult.Error || isSecondaryValidResult == TaskResult.Error)
            {
                return TaskResult.Error;
            }

            LogHelper(LogLevel.Information, "{0}: Primary account ({0} and {1}) and secondary account ({2} and {3}) are both valid.");

            // Return whether or not the primary should be rotated.
            // It is ok to rotate the secondary if it returned TaskResult.ShouldNotRotate because it is not used by live services.
            return isPrimaryValidResult;
        }

        public virtual async Task<TaskResult> IsAccountValid(SqlAccountSecret.Rank rank)
        {
            SqlAccountSecret username, password;
            int usernameLogIndex, passwordLogIndex;
            GetAccountAndLogIndexesFromRank(rank, out username, out password, out usernameLogIndex, out passwordLogIndex);

            try
            {
                await TestSqlConnection(BuildConnectionString(username, password));
                LogHelper(LogLevel.Warning, "{0}: Connected to account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}).");
                return TaskResult.Valid;
            }
            catch (Exception exception)
            {
                // Connecting to account failed! Attempt to connect using temporary secrets for account.
                try
                {
                    LogHelper(LogLevel.Warning, "{0}: Could not connect to account ({" + usernameLogIndex + "} and {" + passwordLogIndex +"}): {6}", exception);

                    var tempUsername = await username.GetTemporary();
                    var tempPassword = await password.GetTemporary();
                    await TestSqlConnection(BuildConnectionString(tempUsername, tempPassword));

                    // The temporary credentials are valid. Update KeyVault and don't rotate!
                    await ReplaceSecretsForAccountWithTemporary(rank);

                    LogHelper(LogLevel.Information, "{0}: Recovered account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}) successfully.");
                    return TaskResult.Recovered;
                }
                catch (Exception exception2)
                {
                    LogHelper(LogLevel.Error, "{0}: Could not connect to account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}) with temporary: {6}", exception2);
                    LogHelper(LogLevel.Error, "{0}: Account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}) is not valid.");
                    return TaskResult.Error;
                }
            }
        }

        public override TaskResult IsSecretOutdated()
        {
            // Only rotate if both the primary username and the primary password are outdated.
            // Otherwise, some services may not have been updated with the latest values.
            // Ignore the secondary account because it doesn't matter if it is outdated because it is not used by live services.
            return PrimaryUsernameSecret.IsOutdated() && PrimaryPasswordSecret.IsOutdated()
                ? TaskResult.Outdated
                : TaskResult.Recent;
        }

        public override async Task<TaskResult> RotateSecret()
        {
            try
            {
                LogHelper(LogLevel.Information, "{0}: Rotating SQL account for account on server {4} and database {5}.");

                var connectionStringBuilder = BuildConnectionString(SecondaryUsernameSecret, SecondaryPasswordSecret);

                try
                {
                    await ReplacePasswordOfSecondary(connectionStringBuilder);
                }
                catch (Exception)
                {
                    throw new ArgumentException($"{PrimaryUsernameSecret.Name}: Cannot connect to the secondary account " +
                                                $"({SecondaryUsernameSecret.Name} and {SecondaryPasswordSecret.Name}).");
                }

                await ReplaceSecretsForAccountWithOtherRank(SqlAccountSecret.Rank.Secondary);

                await GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank.Primary);

                await ReplaceSecretsForAccountWithOtherRank(SqlAccountSecret.Rank.Primary);

                await DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);
                await DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank.Primary);

                LogHelper(LogLevel.Information, "{0}: Successfully rotated SQL account.");
                return TaskResult.Success;
            }
            catch (Exception ex)
            {
                LogHelper(LogLevel.Error, "{0}: Failed to rotate SQL account: {6}", ex);
                return TaskResult.Error;
            }
        }

        private SqlConnectionStringBuilder BuildConnectionString(SecretToRotate username, SecretToRotate password)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = ServerUrl,
                InitialCatalog = DatabaseName,
                UserID = username.Value,
                Password = password.Value
            };
        }

        public virtual async Task TestSqlConnection(SqlConnectionStringBuilder connectionStringBuilder)
        {
            using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
            {
                await sqlConnection.OpenAsync();
            }
        }

        public virtual async Task AlterSqlPassword(SqlConnectionStringBuilder connectionStringBuilder, string newPassword)
        {
            using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
            {
                await sqlConnection.ExecuteAsync($"ALTER LOGIN {connectionStringBuilder.UserID} WITH PASSWORD='{newPassword}' OLD_PASSWORD='{connectionStringBuilder.Password}'");
            }
        }

        private async Task ReplacePasswordOfSecondary(SqlConnectionStringBuilder connectionStringBuilder)
        {
            var newPassword = Membership.GeneratePassword(PasswordLength, PasswordNumberOfAlphanumericalCharacters);

            if (connectionStringBuilder.UserID == PrimaryUsernameSecret.Value &&
                connectionStringBuilder.Password == PrimaryPasswordSecret.Value)
            {
                throw new ArgumentException(
                    $"{PrimaryUsernameSecret.Name}: Attempted to replace the password for the primary account! " +
                    $"Either the secondary account ({SecondaryUsernameSecret.Name} and {SecondaryPasswordSecret.Name}) " +
                    "or the temporary secondary account is identical to the primary account.");
            }

            LogHelper(LogLevel.Information, "{0}: Testing connection for secondary account ({2} and {3}).");
            await TestSqlConnection(connectionStringBuilder);

            await GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);

            LogHelper(LogLevel.Information, "{0}: Changing password for secondary account ({2} and {3}).");
            await AlterSqlPassword(connectionStringBuilder, newPassword);

            // Store the new password in the local Secret.
            SecondaryPasswordSecret.Secret.Value = newPassword;

            LogHelper(LogLevel.Information, "{0}: Storing new password for secondary account ({2} and {3}) in KeyVault.");
            await SecondaryPasswordSecret.Set(newPassword);
        }

        private async Task ReplaceSecretsForAccountWithOtherRank(SqlAccountSecret.Rank rank)
        {
            var otherRank = rank == SqlAccountSecret.Rank.Primary
                ? SqlAccountSecret.Rank.Secondary
                : SqlAccountSecret.Rank.Primary;

            SqlAccountSecret oldUsername, oldPassword, newUsername, newPassword;
            int oldUsernameLogIndex, oldPasswordLogIndex, newUsernameLogIndex, newPasswordLogIndex;

            GetAccountAndLogIndexesFromRank(rank, out oldUsername, out oldPassword, out oldUsernameLogIndex, out oldPasswordLogIndex);
            GetAccountAndLogIndexesFromRank(otherRank, out newUsername, out newPassword, out newUsernameLogIndex, out newPasswordLogIndex);

            LogHelper(LogLevel.Information, "{0}: Replacing account ({" + oldUsernameLogIndex + "} and {" + oldPasswordLogIndex + "}) " +
                                            "with account ({" + newUsernameLogIndex + "} and {" + newPasswordLogIndex + "}).");

            await oldUsername.Set(newUsername.Value);
            await oldPassword.Set(newPassword.Value);
        }

        private async Task ReplaceSecretsForAccountWithTemporary(SqlAccountSecret.Rank rank)
        {
            SqlAccountSecret username, password;
            int usernameLogIndex, passwordLogIndex;
            GetAccountAndLogIndexesFromRank(rank, out username, out password, out usernameLogIndex, out passwordLogIndex);

            var tempUsername = await username.GetTemporary();
            var tempPassword = await password.GetTemporary();

            LogHelper(LogLevel.Information, "{0}: Replacing account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}) with values stored in temporary.");

            await username.Set(tempUsername.Value);
            await password.Set(tempPassword.Value);
        }

        private async Task GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            SqlAccountSecret username, password;
            int usernameLogIndex, passwordLogIndex;
            GetAccountAndLogIndexesFromRank(rank, out username, out password, out usernameLogIndex, out passwordLogIndex);

            LogHelper(LogLevel.Information, "{0}: Generating temporary secrets for account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}).");
            await username.SetTemporary(username.Value);
            await password.SetTemporary(password.Value);
        }

        private async Task DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            SqlAccountSecret username, password;
            int usernameLogIndex, passwordLogIndex;
            GetAccountAndLogIndexesFromRank(rank, out username, out password, out usernameLogIndex, out passwordLogIndex);

            LogHelper(LogLevel.Information, "{0}: Deleting temporary secrets for account ({" + usernameLogIndex + "} and {" + passwordLogIndex + "}).");

            await username.DeleteTemporary();
            await password.DeleteTemporary();
        }
    }
}
