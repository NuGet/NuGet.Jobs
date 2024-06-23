using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Jobs.Validation.PackageSigning.Certificates
{
    public static class PinnedCertificates
    {
        /// <summary>
        /// Fetched from Azure Trusted Signing documentation:
        /// https://www.microsoft.com/pkiops/certs/microsoft%20identity%20verification%20root%20certificate%20authority%202020.crt
        /// SHA-1 fingerprint: f40042e2e5f7e8ef8189fed15519aece42c3bfa2
        /// SHA-256 fingerprint: 5367f20c7ade0e2bca790915056d086b720c33c1fa2a2661acf787e3292e1270
        /// </summary>
        public static X509Certificate2 MicrosoftIdentityVerificationRootCertificateAuthority2020 { get; }

        static PinnedCertificates()
        {
            var prefix = typeof(PinnedCertificates).Namespace;

            MicrosoftIdentityVerificationRootCertificateAuthority2020 =
                GetCertificateFromResource($"{prefix}.{nameof(MicrosoftIdentityVerificationRootCertificateAuthority2020)}.cer");
        }

        private static X509Certificate2 GetCertificateFromResource(string resourceName)
        {
            var assembly = typeof(PinnedCertificates).Assembly;

            using (var memoryStream = new MemoryStream())
            {
                using (var certStream = assembly.GetManifestResourceStream(resourceName))
                {
                    certStream.CopyTo(memoryStream);
                }

                return new X509Certificate2(memoryStream.ToArray());
            }
        }
    }
}
