using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Security;
using Microsoft.Azure.KeyVault;
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
        public const int PasswordLength = 16;

        /// <summary>
        /// Passwords regenerated during rotator have exactly this many nonalphanumeric characters.
        /// </summary>
        public const int PasswordNumberOfNonAlphanumericCharacters = 4;

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
            return secrets.Single(secret => secret != null && secret.Type == type && secret.CurrentRank == rank);
        }

        public SqlAccountSecret GetSecret(SqlAccountSecret.Rank rank, SqlAccountSecret.SqlType type)
        {
            return
                GetSecret(
                    new[]
                        {PrimaryUsernameSecret, PrimaryPasswordSecret, SecondaryUsernameSecret, SecondaryPasswordSecret},
                    rank, type);
        }

        private static readonly Dictionary<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType, bool>, LogKeyword>
            _tupleToKeyword = new Dictionary<Tuple<SqlAccountSecret.Rank, SqlAccountSecret.SqlType, bool>, LogKeyword>
            {
                {
                    Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username, false),
                    LogKeyword.PrimaryUsernameSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password, false),
                    LogKeyword.PrimaryPasswordSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username, false),
                    LogKeyword.SecondaryUsernameSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password, false),
                    LogKeyword.SecondaryPasswordSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Username, true),
                    LogKeyword.TempPrimaryUsernameSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Primary, SqlAccountSecret.SqlType.Password, true),
                    LogKeyword.TempPrimaryPasswordSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Username, true),
                    LogKeyword.TempSecondaryUsernameSecretName
                },
                {
                    Tuple.Create(SqlAccountSecret.Rank.Secondary, SqlAccountSecret.SqlType.Password, true),
                    LogKeyword.TempSecondaryPasswordSecretName
                }
            };

        private static void GetLogKeywordsFromRank(SqlAccountSecret.Rank rank,
            out LogKeyword usernameLogKeyword,
            out LogKeyword passwordLogKeyword)
        {
            try
            {
                usernameLogKeyword = _tupleToKeyword[Tuple.Create(rank, SqlAccountSecret.SqlType.Username, false)];
                passwordLogKeyword = _tupleToKeyword[Tuple.Create(rank, SqlAccountSecret.SqlType.Password, false)];
            }
            catch (KeyNotFoundException)
            {
                usernameLogKeyword = LogKeyword.None;
                passwordLogKeyword = LogKeyword.None;
            }
        }

        private LogKeyword GetLogKeywordFromValue(string value)
        {
            var secrets = new[]
                {PrimaryUsernameSecret, PrimaryPasswordSecret, SecondaryUsernameSecret, SecondaryPasswordSecret};

            try
            {
                var isTemp = false;
                var secret = secrets.Single(secretToCheck =>
                {
                    if (secretToCheck.Value == value)
                    {
                        return true;
                    }

                    try
                    {
                        return isTemp = secretToCheck.GetTemporary().Result.Value == value;
                    }
                    catch
                    {
                        return false;
                    }
                });

                return _tupleToKeyword[Tuple.Create(secret.CurrentRank, secret.Type, isTemp)];
            }
            catch
            {
                return LogKeyword.None;
            }
        }

        private void GetLogKeywordsFromConnectionString(SqlConnectionStringBuilder connectionString,
            out LogKeyword usernameLogKeyword, out LogKeyword passwordLogKeyword)
        {
            usernameLogKeyword = GetLogKeywordFromValue(connectionString.UserID);
            passwordLogKeyword = GetLogKeywordFromValue(connectionString.Password);
        }

        private enum LogKeyword
        {
            PrimaryUsernameSecretName,
            PrimaryPasswordSecretName,
            SecondaryUsernameSecretName,
            SecondaryPasswordSecretName,
            TempPrimaryUsernameSecretName,
            TempPrimaryPasswordSecretName,
            TempSecondaryUsernameSecretName,
            TempSecondaryPasswordSecretName,
            ServerUrl,
            DatabaseName,

            /// <summary> Must be provided when calling LogHelper. </summary>
            Exception,
            None,

            /// <summary> Only used in logging exceptions. </summary>
            LogMessage
        }

        /// <summary>
        /// Logs to Logger by formatting a message with a set of parameters.
        /// </summary>
        /// <param name="logLevel">LogLevel to log with. Only supports Information, Warning, Error, and Critical.</param>
        /// <param name="message">
        /// A formattable string. Look at LogIndex for the values it supports
        /// </param>
        /// <param name="exception">An exception to log (optional).</param>
        private string LogHelper(LogLevel logLevel, string message, Exception exception = null)
        {
            try
            {
                IDictionary<LogKeyword, Func<object>> logDictionary = new Dictionary<LogKeyword, Func<object>>
            {
                {LogKeyword.PrimaryUsernameSecretName, () => PrimaryUsernameSecret.Name},
                {LogKeyword.PrimaryPasswordSecretName, () => PrimaryPasswordSecret.Name},
                {LogKeyword.SecondaryUsernameSecretName, () => SecondaryUsernameSecret.Name},
                {LogKeyword.SecondaryPasswordSecretName, () => SecondaryPasswordSecret.Name},
                {LogKeyword.TempPrimaryUsernameSecretName, () => PrimaryUsernameSecret.GetTemporary().Result.Name},
                {LogKeyword.TempPrimaryPasswordSecretName, () => PrimaryPasswordSecret.GetTemporary().Result.Name},
                {LogKeyword.TempSecondaryUsernameSecretName, () => SecondaryUsernameSecret.GetTemporary().Result.Name},
                {LogKeyword.TempSecondaryPasswordSecretName, () => SecondaryPasswordSecret.GetTemporary().Result.Name},
                {LogKeyword.ServerUrl, () => ServerUrl},
                {LogKeyword.DatabaseName, () => DatabaseName},
                {LogKeyword.Exception, () => exception},
                {LogKeyword.None, () => "?" }
            };

                // Format the message to convert the indices to parameter names.
                // Remember the keywords that are used so that we can include only them in the logger parameters.
                var logMessage = message;
                var outputMessage = message;
                var keywordsInMessage = new List<Tuple<int, object>>();
                foreach (var logKeyword in logDictionary.Keys)
                {
                    var logKeywordString = logKeyword.ToString();

                    if (!logMessage.Contains(logKeywordString))
                    {
                        continue;
                    }

                    // Find every occurrence of the keyword and add it to a list with the index at which it was found.
                    var index = 0;
                    int foundIndex;
                    do
                    {
                        foundIndex = message.IndexOf(logKeywordString, index);
                        index = foundIndex + logKeywordString.Length;
                        if (foundIndex != -1)
                        {
                            keywordsInMessage.Add(Tuple.Create(foundIndex, logDictionary[logKeyword]()));
                        }
                    } while (foundIndex != -1);

                    logMessage = logMessage.Replace(logKeywordString, "{" + logKeywordString + "}");
                    outputMessage = outputMessage.Replace(logKeywordString, logDictionary[logKeyword]().ToString());
                }

                var usedLoggerParams = keywordsInMessage.OrderBy(tuple => tuple.Item1).Select(tuple => tuple.Item2).ToArray();

                switch (logLevel)
                {
                    case LogLevel.Information:
                        Logger.LogInformation(logMessage, usedLoggerParams);
                        break;
                    case LogLevel.Warning:
                        Logger.LogWarning(logMessage, usedLoggerParams);
                        break;
                    case LogLevel.Error:
                        Logger.LogError(logMessage, usedLoggerParams);
                        break;
                    case LogLevel.Critical:
                        Logger.LogCritical(logMessage, usedLoggerParams);
                        break;
                }

                return outputMessage;
            }
            catch (Exception logException)
            {
                Logger.LogError("Threw exception while logging: {" + LogKeyword.Exception + "}", logException);
                Logger.LogWarning("Attempted to log: {" + LogKeyword.LogMessage + "}", message);
                return message;
            }
        }

        public override async Task<TaskResult> IsSecretValid()
        {
            LogHelper(LogLevel.Information,
                $"{LogKeyword.PrimaryUsernameSecretName}: Validating SQL account with server {LogKeyword.ServerUrl} and database {LogKeyword.DatabaseName}.");

            var isPrimaryValidResult = await IsAccountValid(SqlAccountSecret.Rank.Primary);
            var isSecondaryValidResult = await IsAccountValid(SqlAccountSecret.Rank.Secondary);

            // If either the primary or the secondary account could not be validated, return TaskResult.Error!
            if (isPrimaryValidResult == TaskResult.Error || isSecondaryValidResult == TaskResult.Error)
            {
                return TaskResult.Error;
            }

            LogHelper(LogLevel.Information,
                $"{LogKeyword.PrimaryUsernameSecretName}: Primary account ({LogKeyword.PrimaryUsernameSecretName} and {LogKeyword.PrimaryPasswordSecretName}) and secondary account ({LogKeyword.SecondaryUsernameSecretName} and {LogKeyword.SecondaryPasswordSecretName}) are both valid.");

            // Return whether or not the primary should be rotated.
            // It is ok to rotate the secondary if it returned TaskResult.ShouldNotRotate because it is not used by live services.
            return isPrimaryValidResult;
        }

        public virtual async Task<TaskResult> IsAccountValid(SqlAccountSecret.Rank rank)
        {
            var usernameSecret = GetSecret(rank, SqlAccountSecret.SqlType.Username);
            var passwordSecret = GetSecret(rank, SqlAccountSecret.SqlType.Password);
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromRank(rank, out usernameLogKeyword, out passwordLogKeyword);

            try
            {
                if (rank == SqlAccountSecret.Rank.Secondary &&
                    PrimaryUsernameSecret.Value == SecondaryUsernameSecret.Value &&
                    PrimaryPasswordSecret.Value == SecondaryPasswordSecret.Value)
                {
                    // If the secondary and primary secrets are identical, then attempt to restore the actual secondary account from temporary secrets.
                    throw new ArgumentException(LogHelper(LogLevel.Error,
                        $"{LogKeyword.PrimaryUsernameSecretName}: Primary account ({LogKeyword.PrimaryUsernameSecretName} and {LogKeyword.PrimaryPasswordSecretName}) and secondary account ({LogKeyword.SecondaryUsernameSecretName} and {LogKeyword.SecondaryPasswordSecretName}) are identical!"));
                }

                await TestSqlConnection(BuildConnectionString(usernameSecret, passwordSecret));
                return TaskResult.Valid;
            }
            catch
            {
                try
                {
                    LogHelper(LogLevel.Warning,
                        $"{LogKeyword.PrimaryUsernameSecretName}: Attempting to connect to account ({usernameLogKeyword} and {passwordLogKeyword}) using temporary secrets.");

                    var tempUsername = await usernameSecret.GetTemporary();
                    var tempPassword = await passwordSecret.GetTemporary();
                    await TestSqlConnection(BuildConnectionString(tempUsername, tempPassword));

                    // The temporary credentials are valid. Update KeyVault and don't rotate!
                    await ReplaceSecretsForAccountWithTemporary(rank);

                    LogHelper(LogLevel.Information,
                        $"{LogKeyword.PrimaryUsernameSecretName}: Recovered account ({usernameLogKeyword} and {passwordLogKeyword}) successfully.");
                    return TaskResult.Recovered;
                }
                catch
                {
                    LogHelper(LogLevel.Error,
                        $"{LogKeyword.PrimaryUsernameSecretName}: Account ({usernameLogKeyword} and {passwordLogKeyword}) is not valid.");
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
                LogHelper(LogLevel.Information,
                    $"{LogKeyword.PrimaryUsernameSecretName}: Rotating SQL account for account on server {LogKeyword.ServerUrl} and database {LogKeyword.DatabaseName}.");

                var connectionStringBuilder = BuildConnectionString(SecondaryUsernameSecret, SecondaryPasswordSecret);
                await ChangePasswordOfAccount(connectionStringBuilder);

                await SwapSecretsForAccounts();

                LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Successfully rotated SQL account.");
                return TaskResult.Success;
            }
            catch (Exception ex)
            {
                LogHelper(LogLevel.Error, $"{LogKeyword.PrimaryUsernameSecretName}: Failed to rotate SQL account: {LogKeyword.Exception}", ex);
                return TaskResult.Error;
            }
        }

        public SqlConnectionStringBuilder BuildConnectionString(SecretToRotate username, SecretToRotate password)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = ServerUrl,
                InitialCatalog = "master", // we must be able to connect to master in order to change the password
                UserID = username.Value,
                Password = password.Value
            };
        }

        public virtual async Task TestSqlConnection(SqlConnectionStringBuilder connectionStringBuilder)
        {
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromConnectionString(connectionStringBuilder, out usernameLogKeyword, out passwordLogKeyword);
            LogHelper(LogLevel.Information,
                $"{LogKeyword.PrimaryUsernameSecretName}: Testing connection for account ({usernameLogKeyword} and {passwordLogKeyword}).");

            try
            {
                using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
                {
                    await sqlConnection.OpenAsync();
                }

                LogHelper(LogLevel.Information,
                    $"{LogKeyword.PrimaryUsernameSecretName}: Successfully connected to account ({usernameLogKeyword} and {passwordLogKeyword})!");
            }
            catch (Exception exception)
            {
                LogHelper(LogLevel.Warning,
                    $"{LogKeyword.PrimaryUsernameSecretName}: Could not connect to account ({usernameLogKeyword} and {passwordLogKeyword}): {LogKeyword.Exception}",
                    exception);
                throw;
            }
        }

        public virtual async Task ChangeSqlPasswordOfAccount(SqlConnectionStringBuilder connectionStringBuilder,
            string newPassword)
        {
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromConnectionString(connectionStringBuilder, out usernameLogKeyword, out passwordLogKeyword);
            LogHelper(LogLevel.Information,
                $"{LogKeyword.PrimaryUsernameSecretName}: Changing password for account ({usernameLogKeyword} and {passwordLogKeyword}).");

            try
            {
                using (var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString))
                {
                    await sqlConnection.OpenAsync();
                    // This query must run on the master database to succeed!
                    await sqlConnection.ExecuteAsync($"ALTER LOGIN {connectionStringBuilder.UserID} WITH PASSWORD='{newPassword}' OLD_PASSWORD='{connectionStringBuilder.Password}'");
                }

                LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Successfully changed password for account ({usernameLogKeyword} and {passwordLogKeyword}).");
            }
            catch (Exception exception)
            {
                LogHelper(LogLevel.Warning,
                    $"{LogKeyword.PrimaryUsernameSecretName}: Could not change password for account ({usernameLogKeyword} and {passwordLogKeyword}): {LogKeyword.Exception}", exception);
                throw;
            }
        }

        public virtual async Task ChangePasswordOfAccount(SqlConnectionStringBuilder connectionStringBuilder)
        {
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromConnectionString(connectionStringBuilder, out usernameLogKeyword, out passwordLogKeyword);
            LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Changing the password for account ({usernameLogKeyword} and {passwordLogKeyword}).");

            if (connectionStringBuilder.UserID == PrimaryUsernameSecret.Value &&
                connectionStringBuilder.Password == PrimaryPasswordSecret.Value)
            {
                throw new ArgumentException(LogHelper(LogLevel.Warning, $"{LogKeyword.PrimaryUsernameSecretName}: Attempted to replace the password for the primary account! This is not safe because it may be in use by services! Either the secondary account ({LogKeyword.SecondaryUsernameSecretName} and {LogKeyword.SecondaryPasswordSecretName}) or the temporary secondary account is identical to the primary account."));
            }
            
            await TestSqlConnection(connectionStringBuilder);
            
            // Save the old login in temporary secrets in case there is an error in the process.
            await GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);

            // Set the secret in KeyVault to the desired new password BEFORE changing the password on the SQL server!
            //
            // We do this in this order because if we changed the actual login on the SQL server first (AlterSqlPassword),
            // but then failed to store the new value in KeyVault (connection issues, etc), the new password would be lost!
            //
            // In the case that AlterSqlPassword fails, we can retrieve the old login from the temporary secrets.
            var newPassword = Membership.GeneratePassword(PasswordLength, PasswordNumberOfNonAlphanumericCharacters);
            await SecondaryPasswordSecret.Set(newPassword);
            
            await ChangeSqlPasswordOfAccount(connectionStringBuilder, newPassword);

            // Delete the old login from temporary secrets because it is no longer needed.
            await DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);
        }

        public virtual async Task SwapSecretsForAccounts()
        {
            LogHelper(LogLevel.Information,
                $"{LogKeyword.PrimaryUsernameSecretName}: Swapping primary account ({LogKeyword.PrimaryUsernameSecretName} and {LogKeyword.PrimaryPasswordSecretName}) and secondary account ({LogKeyword.SecondaryUsernameSecretName} and {LogKeyword.SecondaryPasswordSecretName}).");

            await GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank.Primary);
            await GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);

            var oldSecondaryUsername = SecondaryUsernameSecret.Value;
            var oldSecondaryPassword = SecondaryPasswordSecret.Value;

            // Replace the secondary account first because if it fails the primary account remains intact.
            await SecondaryUsernameSecret.Set(PrimaryUsernameSecret.Value);
            await SecondaryPasswordSecret.Set(PrimaryPasswordSecret.Value);

            await PrimaryUsernameSecret.Set(oldSecondaryUsername);
            await PrimaryPasswordSecret.Set(oldSecondaryPassword);

            await DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank.Primary);
            await DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank.Secondary);
        }

        public virtual async Task ReplaceSecretsForAccountWithTemporary(SqlAccountSecret.Rank rank)
        {
            var usernameSecret = GetSecret(rank, SqlAccountSecret.SqlType.Username);
            var passwordSecret = GetSecret(rank, SqlAccountSecret.SqlType.Password);
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromRank(rank, out usernameLogKeyword, out passwordLogKeyword);

            LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Replacing account ({usernameLogKeyword} and {passwordLogKeyword}) with values stored in temporary.");

            await usernameSecret.Set((await usernameSecret.GetTemporary()).Value);
            await passwordSecret.Set((await passwordSecret.GetTemporary()).Value);

            // Delete the temporary secrets because they are no longer needed.
            await DeleteTemporarySecretsForAccount(rank);
        }

        public virtual async Task GenerateTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            var usernameSecret = GetSecret(rank, SqlAccountSecret.SqlType.Username);
            var passwordSecret = GetSecret(rank, SqlAccountSecret.SqlType.Password);
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromRank(rank, out usernameLogKeyword, out passwordLogKeyword);

            LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Generating temporary secrets for account ({usernameLogKeyword} and {passwordLogKeyword}).");

            await usernameSecret.SetTemporary(usernameSecret.Value);
            await passwordSecret.SetTemporary(passwordSecret.Value);
        }

        public virtual async Task DeleteTemporarySecretsForAccount(SqlAccountSecret.Rank rank)
        {
            var usernameSecret = GetSecret(rank, SqlAccountSecret.SqlType.Username);
            var passwordSecret = GetSecret(rank, SqlAccountSecret.SqlType.Password);
            LogKeyword usernameLogKeyword, passwordLogKeyword;
            GetLogKeywordsFromRank(rank, out usernameLogKeyword, out passwordLogKeyword);

            LogHelper(LogLevel.Information, $"{LogKeyword.PrimaryUsernameSecretName}: Deleting temporary secrets for account ({usernameLogKeyword} and {passwordLogKeyword}).");

            await usernameSecret.DeleteTemporary();
            await passwordSecret.DeleteTemporary();
        }
    }
}
