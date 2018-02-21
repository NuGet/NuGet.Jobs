using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.PackageSigning
{
    public class PackageValidationUtility
    {
        private const int BufferSize = 8192;

        public static async Task<Stream> DownloadPackageAsync(HttpClient httpClient, Uri packageUri, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation("Attempting to download package from {PackageUri}...", packageUri);

            Stream packageStream = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Download the package from the network to a temporary file.
                using (var response = await httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead))
                {
                    logger.LogInformation(
                        "Received response {StatusCode}: {ReasonPhrase} of type {ContentType} for request {PackageUri}",
                        response.StatusCode,
                        response.ReasonPhrase,
                        response.Content.Headers.ContentType,
                        packageUri);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new InvalidOperationException($"Expected status code {HttpStatusCode.OK} for package download, actual: {response.StatusCode}");
                    }

                    using (var networkStream = await response.Content.ReadAsStreamAsync())
                    {
                        packageStream = new FileStream(
                                            Path.GetTempFileName(),
                                            FileMode.Create,
                                            FileAccess.ReadWrite,
                                            FileShare.None,
                                            BufferSize,
                                            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

                        await networkStream.CopyToAsync(packageStream, BufferSize, cancellationToken);
                    }
                }

                packageStream.Position = 0;

                logger.LogInformation(
                    "Downloaded {PackageSizeInBytes} bytes in {DownloadElapsedTime} seconds for request {PackageUri}",
                    packageStream.Length,
                    stopwatch.Elapsed.TotalSeconds,
                    packageUri);

                return packageStream;
            }
            catch (Exception e)
            {
                logger.LogError(
                    Error.ValidateSignatureFailedToDownloadPackageStatus,
                    e,
                    "Exception thrown when trying to download package from {PackageUri}",
                    packageUri);

                packageStream?.Dispose();

                throw;
            }
        }
    }
}
