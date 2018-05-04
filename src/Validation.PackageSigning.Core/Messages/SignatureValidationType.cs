namespace NuGet.Jobs.Validation.PackageSigning.Messages
{
    public enum SignatureValidationType
    {
        /// <summary>
        /// Validate the package's signature, if any. Unacceptable repository signatures will
        /// be stripped.
        /// </summary>
        ProcessSignature = 0,

        /// <summary>
        /// Validate the package's signature, without modifying the package. The package MUST
        /// have an acceptable repository signature to pass validation.
        /// </summary>
        ValidateSignature = 1,
    }
}
