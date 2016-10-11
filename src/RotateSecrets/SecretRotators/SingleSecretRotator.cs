using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using RotateSecrets.SecretsToRotate;

namespace RotateSecrets.SecretRotators
{
    public abstract class SingleSecretRotator
        : SecretRotator
    {
        public SecretToRotate Secret { get; }

        protected SingleSecretRotator(SecretToRotate secretToRotate)
        {
            Secret = secretToRotate;
        }

        public override TaskResult IsSecretOutdated()
        {
            return Secret.IsOutdated() ? TaskResult.Outdated : TaskResult.Recent;
        }
    }
}
