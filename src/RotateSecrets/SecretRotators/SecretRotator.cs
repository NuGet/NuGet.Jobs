using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace RotateSecrets.SecretRotators
{
    public abstract class SecretRotator
    {
        public enum TaskResult
        {
            /// <summary>
            /// An exception was thrown. The secret may be in a bad state and cannot be recovered.
            /// Final state.
            /// </summary>
            Error,

            /// <summary>
            /// Not a secret that was marked to be rotated.
            /// If it is intended to be rotated, this secret is probably missing some tags or its tags are invalid.
            /// </summary>
            Ignore,

            /// <summary>
            /// The secret is valid and can be rotated.
            /// Intermediate state.
            /// </summary>
            Valid,

            /// <summary>
            /// The secret is now valid but needed to be recovered from a bad state and should not be rotated.
            /// Final state.
            /// </summary>
            Recovered,

            /// <summary>
            /// The secret is outdated and should be rotated.
            /// Intermediate state.
            /// </summary>
            Outdated,

            /// <summary>
            /// The secret is new and does not need to be rotated.
            /// Final state.
            /// </summary>
            Recent,

            /// <summary>
            /// The secret was successfully rotated.
            /// Final state.
            /// </summary>
            Success
        }

        protected ILogger Logger { get; }

        protected SecretRotator()
        {
            Logger = JobRunner.ServiceContainer.GetService<ILoggerFactory>().CreateLogger(GetType());
        }

        /// <summary>
        /// Validates and then rotates the secret if the secret is outdated.
        /// </summary>
        /// <returns></returns>
        public async Task<TaskResult> ProcessSecret()
        {
            var isValid = await IsSecretValid();
            if (isValid != TaskResult.Valid)
            {
                return isValid;
            }

            var isOutdated = IsSecretOutdated();
            if (isOutdated != TaskResult.Outdated)
            {
                return isOutdated;
            }

            return await RotateSecret();
        }

        /// <summary>
        /// Check if the secret is a valid credential. If it is not, attempt to recover it.
        /// </summary>
        /// 
        /// <returns>
        /// True if the secret was valid.
        /// True if the secret was invalid but the correct secret was recovered.
        /// False if the secret was invalid and recovery failed.
        /// </returns>
        public abstract Task<TaskResult> IsSecretValid();

        /// <summary>
        /// Check if the secret is outdated and needs to be regenerated.
        /// </summary>
        /// <returns>Whether or not the secret is outdated.</returns>
        public abstract TaskResult IsSecretOutdated();

        /// <summary>
        /// Regenerates the secret and stores it in KeyVault.
        /// </summary>
        /// <returns>Whether or not the secret was successfully rotated.</returns>
        public abstract Task<TaskResult> RotateSecret();
    }
}
