using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;

namespace RotateSecrets.SecretsToRotate
{
    public class StorageAccountAccessKeySecret
        : SecretToRotate
    {
        public string ResourceGroup { get; }
        public string StorageAccountName { get; }

        public StorageAccountAccessKeySecret(Secret secret) : base(secret)
        {
            ResourceGroup = Secret.Tags[Tags.StorageAccountResourceGroup];
            StorageAccountName = Secret.Tags[Tags.StorageAccountName];
        }
    }
}
