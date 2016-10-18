using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Management.Storage;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.KeyVault;

namespace RotateSecrets
{
    public class Utils
    {
        private static Utils _instance;

        /// <summary>
        /// Using the singleton pattern to make it possible to test.
        /// </summary>
        public static Utils Instance
        {
            get
            {
                _instance = _instance ?? new Utils();
                return _instance;
            }
            set
            {
                // Used in testing
                _instance = value;
            }
        }

        private ILogger Logger { get; }

        private Utils()
        {
            Logger = JobRunner.ServiceContainer.GetService<ILoggerFactory>().CreateLogger(GetType());
        }

        public virtual StorageManagementClient StorageClient
            => JobRunner.ServiceContainer.GetService<StorageManagementClient>();
        public virtual KeyVaultConfiguration VaultConfig => JobRunner.ServiceContainer.GetService<KeyVaultConfiguration>();
        public virtual KeyVaultClient VaultClient => JobRunner.ServiceContainer.GetService<KeyVaultReader>().KeyVaultClient;

        public virtual string GetVaultUrl()
        {
            return $"https://{VaultConfig.VaultName}.vault.azure.net/";
        }

        public virtual async Task<IEnumerable<SecretItem>> ListSecrets()
        {
            return (await VaultClient.GetSecretsAsync(GetVaultUrl())).Value;
        }

        public virtual Task<Secret> GetSecret(string secretName)
        {
            return VaultClient.GetSecretAsync(GetVaultUrl(), secretName);
        }

        public virtual Task SetSecret(Secret secret, string value, Dictionary<string, string> tags = null)
        {
            return SetSecret(secret.SecretIdentifier.Name, value, tags ?? secret.Tags);
        }

        public virtual async Task SetSecret(string name, string value,
            Dictionary<string, string> tags = null)
        {
            await VaultClient.SetSecretAsync(GetVaultUrl(), name, value, tags);
            Logger.LogInformation("Set value of {SecretName} in KeyVault.", name);
        }

        public virtual Task DeleteSecret(Secret secret)
        {
            return DeleteSecret(secret.SecretIdentifier.Name);
        }

        public virtual async Task DeleteSecret(string name)
        {
            await VaultClient.DeleteSecretAsync(GetVaultUrl(), name);
            Logger.LogInformation("Deleted {SecretName} from KeyVault.", name);
        }

        public virtual string GetTemporarySecretName(Secret secret)
        {
            return secret.SecretIdentifier.Name + "TEMP";
        }

        public virtual async Task<Secret> GetTemporarySecret(Secret secret)
        {
            return await GetSecret(GetTemporarySecretName(secret));
        }
    }
}
