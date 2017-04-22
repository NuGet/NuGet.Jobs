using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    public static class ValidationEvent
    {
        /// <summary>
        /// Virus scan request is about to be sent
        /// </summary>
        public const string BeforeVirusScanRequest = "BeforeVirusScan";

        /// <summary>
        /// The validation queue item was deadlettered after several attempts of processing
        /// </summary>
        public const string Deadlettered = "Deadlettered";

        /// <summary>
        /// The detail item passed with a <see cref="PackageNotClean"/> result
        /// </summary>
        public const string NotCleanReason = "NotCleanReason";

        /// <summary>
        /// Virus scan service reported package as clean
        /// </summary>
        public const string PackageClean = "PackageClean";

        /// <summary>
        /// Packages download was successful
        /// </summary>
        public const string PackageDownloaded = "PackageDownloaded";

        /// <summary>
        /// Virus scan service reported package as not clean
        /// </summary>
        public const string PackageNotClean = "PackageNotClean";

        /// <summary>
        /// Virus scan service reported its failure to scan package (it does *not* mean package is not clean)
        /// </summary>
        public const string ScanFailed = "ScanFailed";

        /// <summary>
        /// The detail item passed with <see cref="ScanFailed"/> result
        /// </summary>
        public const string ScanFailureReason = "ScanFailureReason";

        /// <summary>
        /// An exception was thrown during validator execution
        /// </summary>
        public const string ValidatorException = "ValidatorException";

        /// <summary>
        /// The virus scan request was submitted
        /// </summary>
        public const string VirusScanRequestSent = "VirusScanRequestSent";

        /// <summary>
        /// Sending the virus scanning request had failed
        /// </summary>
        public const string VirusScanRequestFailed = "VirusScanRequestFailed";

        /// <summary>
        /// Package was successfully unzipped
        /// </summary>
        public const string UnzipSucceeeded = "UnzipSucceeeded";
    }
}
