using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using RotateSecrets.SecretRotators;

namespace RotateSecrets.SecretsToRotate
{
    public class SecretToRotate
    {
        public virtual Secret Secret { get; }

        public virtual string Name => Secret.SecretIdentifier.Name;
        public virtual string Value
        {
            get { return Secret.Value; }
            set { Secret.Value = value; }
        }

        /// <summary>
        /// The timestamp before which this secret should be considered outdated.
        /// </summary>
        public DateTime SecretOutdatedTimestamp { get; }

        public static async Task<SecretToRotate> Get(string secretName)
        {
            return new SecretToRotate(await Utils.Instance.GetSecret(secretName));
        }

        public SecretToRotate(Secret secret)
        {
            Secret = secret;
            SecretOutdatedTimestamp = DateTime.UtcNow.Subtract(new TimeSpan(SecretRotatorFactory.Instance.SecretLongevityDays, 0, 0));
        }

        public virtual bool IsOutdated()
        {
            return Utils.Instance.IsSecretOutdated(Secret, SecretOutdatedTimestamp);
        }

        public virtual Task Set(string value)
        {
            return Utils.Instance.SetSecret(Secret, value);
        }

        public virtual Task Delete()
        {
            return Utils.Instance.DeleteSecret(Secret);
        }

        public virtual async Task<SecretToRotate> GetTemporary()
        {
            return new SecretToRotate(await Utils.Instance.GetTemporarySecret(Secret));
        }

        public virtual Task SetTemporary(string value)
        {
            return Utils.Instance.SetSecret(Utils.Instance.GetTemporarySecretName(Secret), Value);
        }

        public virtual Task DeleteTemporary()
        {
            return Utils.Instance.DeleteSecret(Utils.Instance.GetTemporarySecretName(Secret));
        }
    }
}
