using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Extensions.Logging;
using RotateSecrets.SecretsToRotate;

namespace RotateSecrets.SecretRotators
{
    public class StorageAccountRotator
        : SingleSecretRotator
    {
        public string ResourceGroup { get; }
        public string StorageAccountName { get; }

        public StorageAccountRotator(StorageAccountAccessKeySecret secret)
            : base(secret)
        {
            ResourceGroup = secret.ResourceGroup;
            StorageAccountName = secret.StorageAccountName;
        }

        /// <summary>
        /// Checks and compares the access key stored in KeyVault with the valid access keys.
        /// </summary>
        /// <returns>A nullable boolean. True if access key in KeyVault is primary access key, false if key is secondary access key, and null if the key is neither.</returns>
        /// <returns>The response returned by the GetKeysAsync request.</returns>
        private async Task<Tuple<bool?, StorageAccountListKeysResult>> CheckKeysInVault()
        {
            try
            {
                var currentAccessKey = Secret.Value;
                var accessKeysResponse = await Utils.Instance.StorageClient.StorageAccounts.ListKeysAsync(ResourceGroup, StorageAccountName);

                // Find what key (primary or secondary) is currently in the KeyVault
                var isPrimaryKeyInVault = new bool?();
                if (accessKeysResponse.Keys[0].Value == currentAccessKey)
                {
                    isPrimaryKeyInVault = true;
                }
                else if (accessKeysResponse.Keys[1].Value == currentAccessKey)
                {
                    isPrimaryKeyInVault = false;
                }

                return Tuple.Create(isPrimaryKeyInVault, accessKeysResponse);
            }
            catch (Exception ex)
            {
                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Failed to list the keys for storage account {StorageAccountName}: {Exception}", Secret.Name, StorageAccountName, ex);
                throw;
            }
        }

        public override async Task<TaskResult> IsSecretValid()
        {
            try
            {
                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Validating storage account access key for storage account {StorageAccountName}.",
                    Secret.Name, StorageAccountName);

                var keysInVaultTuple = await CheckKeysInVault();
                var isPrimaryKeyInVault = keysInVaultTuple.Item1;
                var listKeysResponse = keysInVaultTuple.Item2;

                if (!isPrimaryKeyInVault.HasValue)
                {
                    // This should not happen unless someone besides the job has tampered with the KeyVault.
                    // The job only rotates the key that's NOT in KeyVault, so the job can never get into a state 
                    // where KeyVault stores an invalid key, even when storing the new access key in KeyVault fails!
                    // If the key in KeyVault is not a valid key, then that key must have been changed between runs of the job.
                    Logger.LogWarning("{StorageAccountAccessKeySecretName}: is not the primary or secondary access key for the storage account {StorageAccountName}!",
                        Secret.Name, StorageAccountName);

                    // Store the current primary in KeyVault and don't rotate!
                    await Secret.Set(listKeysResponse.Keys[0].Value);
                    return TaskResult.Recovered;
                }

                Logger.LogInformation("{StorageAccountAccessKeySecretName}: is the {KeyName} access key.", Secret.Name, isPrimaryKeyInVault.Value ? "primary" : "secondary");
                return TaskResult.Valid;
            }
            catch (Exception ex)
            {
                Logger.LogError("{StorageAccountAccessKeySecretName}: Failed to validate storage account access key: {Exception}", Secret.Name, ex);
                return TaskResult.Error;
            }
        }

        public override async Task<TaskResult> RotateSecret()
        {
            try
            {
                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Rotating storage account access key for storage account {StorageAccountName}.",
                    Secret.Name, StorageAccountName);
                
                var keysInVaultTuple = await CheckKeysInVault();
                var isPrimaryKeyInVault = keysInVaultTuple.Item1;
                var listKeysResponse = keysInVaultTuple.Item2;

                if (!isPrimaryKeyInVault.HasValue)
                {
                    // This should not happen because it implies the secret is not valid but it proceeded to RotateSecret anyway.
                    throw new ArgumentException(
                        $"{Secret.Name} is not the primary or secondary access key for the storage account named {StorageAccountName}!");
                }

                // Regenerate the key that is NOT currently in the KeyVault
                var regeneratedKeyName = isPrimaryKeyInVault.Value ? "secondary" : "primary";
                var regeneratedKeyIndex = isPrimaryKeyInVault.Value ? 1 : 0;

                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Regenerating {RegeneratedKeyName} access key of the storage account named {StorageAccountName}.",
                    Secret.Name, regeneratedKeyName, StorageAccountName);
                
                var regenerateKeysResponse =
                    await
                        Utils.Instance.StorageClient.StorageAccounts.RegenerateKeyAsync(ResourceGroup, StorageAccountName,
                            listKeysResponse.Keys[regeneratedKeyIndex].KeyName);

                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Storing {RegeneratedKeyName} access key of the storage account {StorageAccountName} in KeyVault.",
                    Secret.Name, regeneratedKeyName, StorageAccountName);

                var newAccessKey = regenerateKeysResponse.Keys[regeneratedKeyIndex].Value;
                await Secret.Set(newAccessKey);

                Logger.LogInformation("{StorageAccountAccessKeySecretName}: Successfully rotated storage account access key.", Secret.Name);
                return TaskResult.Success;
            }
            catch (Exception ex)
            {
                Logger.LogError("{StorageAccountAccessKeySecretName}: Failed to rotate storage account access key: {Exception}", Secret.Name, ex);
                return TaskResult.Error;
            }
        }
    }
}
