using System;
using System.Collections.Generic;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using RotateSecrets.SecretsToRotate;

namespace RotateSecrets.SecretRotators
{
    /// <summary>
    /// A factory that takes in a secret and returns a SecretRotator created from that secret.
    /// </summary>
    public class SecretRotatorFactory
    {
        private static SecretRotatorFactory _instance;

        /// <summary>
        /// Using the singleton pattern to make it possible to test.
        /// </summary>
        public static SecretRotatorFactory Instance
        {
            get
            {
                _instance = _instance ?? new SecretRotatorFactory();
                return _instance;
            }
        }
        
        /// <summary>
        /// Default number of days before a secret becomes outdated.
        /// </summary>
        public const int DefaultSecretLongevityDays = 7;

        public int SecretLongevityDays { get; set; } = DefaultSecretLongevityDays;

        private readonly IDictionary<string, IList<SqlAccountSecret>> _unmatchedSqlAccountSecrets = new Dictionary<string, IList<SqlAccountSecret>>();
        private readonly ILogger _logger;

        private SecretRotatorFactory()
        {
            _logger = JobRunner.ServiceContainer.GetService<ILoggerFactory>().CreateLogger(typeof(SecretRotatorFactory));
        }

        public bool IsAllSecretsRotated()
        {
            foreach (var primaryUserNameSecretName in _unmatchedSqlAccountSecrets.Keys)
            {
                _logger.LogWarning("Cannot find a full group of SQL account secrets associated with {PrimaryUserNameSecretName}!", primaryUserNameSecretName);
                foreach (var secret in _unmatchedSqlAccountSecrets[primaryUserNameSecretName])
                {
                    _logger.LogWarning("{SecretName} is associated with {PrimaryUserNameSecretName}", secret.Name, primaryUserNameSecretName);
                }
            }

            return _unmatchedSqlAccountSecrets.Count == 0;
        }

        /// <summary>
        /// Creates a SecretRotator from a secret.
        /// </summary>
        /// <param name="secret">A secret to create a SecretRotator from.</param>
        /// <returns>
        /// A SecretRotator formed from secret.
        /// Returns an EmptySecretRotator if the secret cannot be converted into a SecretRotator.
        /// If the secret is a part of set of secrets that form a SecretRotator, only the last call of GetSecretRotator with a secret in the set returns the SecretRotator.
        /// </returns>
        public SecretRotator GetSecretRotator(Secret secret)
        {
            // Only rotate secrets if they have the ShouldRotate tag and it is set to "True".
            string shouldRotate;
            if (secret.Tags == null || !secret.Tags.TryGetValue(Tags.ShouldRotate, out shouldRotate) || !Convert.ToBoolean(shouldRotate))
            {
                return new EmptySecretRotator();
            }

            // Determine how to rotate the secret by parsing it's Type tag.
            string type;
            if (!secret.Tags.TryGetValue(Tags.Type, out type))
            {
                return new EmptySecretRotator();
            }

            switch (type)
            {
                case Tags.TypeSqlAccount:
                    _logger.LogInformation("Found SQL account secret to rotate named {SecretName}.", secret.SecretIdentifier.Name);

                    var sqlAccountSecret = new SqlAccountSecret(secret);
                    var matchingIndex = sqlAccountSecret.PrimaryUsernameSecretName;
                    IList<SqlAccountSecret> matchingSecrets;

                    if (_unmatchedSqlAccountSecrets.TryGetValue(matchingIndex,
                        out matchingSecrets))
                    {
                        if (matchingSecrets.Count < 3)
                        {
                            _logger.LogInformation("Adding {SecretName} to the group of secrets associated with {PrimaryUsernameSecretName}.", secret.SecretIdentifier.Name, matchingIndex);

                            matchingSecrets.Add(sqlAccountSecret);
                        }
                        else
                        {
                            _logger.LogInformation("{SecretName} completes the group of secrets associated with {PrimaryUsernameSecretName}.", secret.SecretIdentifier.Name, matchingIndex);

                            _unmatchedSqlAccountSecrets.Remove(matchingIndex);
                            return new SqlAccountRotator(matchingSecrets);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("{SecretName} is the first secret associated with {PrimaryUsernameSecretName}.", secret.SecretIdentifier.Name, matchingIndex);

                        _unmatchedSqlAccountSecrets.Add(matchingIndex, new List<SqlAccountSecret> { sqlAccountSecret });
                    }

                    return new EmptySecretRotator();
                case Tags.TypeStorageAccountAccessKey:
                    _logger.LogInformation("Found storage account access key to rotate named {SecretName}.", secret.SecretIdentifier.Name);

                    return new StorageAccountRotator(new StorageAccountAccessKeySecret(secret));
                default:
                    throw new ArgumentException($"SqlType of secret with name {secret.SecretIdentifier.Name} is unsupported ({type})!");
            }
        }
    }
}
