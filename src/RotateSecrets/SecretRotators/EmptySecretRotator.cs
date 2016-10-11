using System.Threading.Tasks;

namespace RotateSecrets.SecretRotators
{
    /// <summary>
    /// An empty implementation of SecretRotator that always returns TaskResult.Ignore.
    /// </summary>
    public class EmptySecretRotator
        : SecretRotator
    {
        public override Task<TaskResult> IsSecretValid()
        {
            return Task.FromResult(TaskResult.Ignore);
        }

        public override TaskResult IsSecretOutdated()
        {
            return TaskResult.Ignore;
        }

        public override Task<TaskResult> RotateSecret()
        {
            return Task.FromResult(TaskResult.Ignore);
        }
    }
}
