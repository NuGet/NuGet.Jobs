using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using NuGetGallery;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;

namespace NuGet.Services.Validation.Orchestrator
{
    public abstract class ValidationFileService : IFileService
    {

        /// <summary>
        /// The value picked today is based off of the maximum duration we wait when downloading packages using the
        /// <see cref="IFileDownloader"/>.
        /// </summary>
        private static readonly TimeSpan AccessDuration = TimeSpan.FromMinutes(10);

        private readonly IFileDownloader _packageDownloader;
        private readonly ILogger<ValidationFileService> _logger;
        ICoreFileStorageService _fileStorageService;
        string _pathTemplate;
        string _extension;
        string _folderName;


        public ValidationFileService(
            ICoreFileStorageService fileStorageService,
            IFileDownloader packageDownloader,
            ILogger<ValidationFileService> logger,
            string pathTemplate,
            string extension,
            string folderName) 
        {
            _fileStorageService = fileStorageService;
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pathTemplate = pathTemplate;
             _extension = extension;
             _folderName = folderName;
        }

        //the Core method if public 
        public string BuildFileName(PackageValidationSet validationSet, string pathTemplate, string extension)
        {
            string id = validationSet.PackageId;
            string version = validationSet.PackageNormalizedVersion;

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) we don't want to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return String.Format(
                CultureInfo.InvariantCulture,
                pathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }
    
        

        public Task<Uri> GetPackageReadUriAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, _pathTemplate, _extension);
            return _fileStorageService.GetFileReadUriAsync(_folderName, fileName, endOfAccess: null);
        }

        public Task DeleteValidationPackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(
                validationSet,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return _fileStorageService.DeleteFileAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task<bool> DoesPackageFileExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.FileExistsAsync(CoreConstants.PackagesFolderName, fileName);
        }


        public async Task StorePackageFileInBackupLocationAsync(PackageValidationSet validationSet, Stream packageFile)
        {
            await Task<bool>.FromResult (true);
        }

        public Task<bool> DoesValidationPackageFileExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.FileExistsAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task DeletePackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(validationSet, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.DeleteFileAsync(CoreConstants.PackagesFolderName, fileName);
        }


        /// <summary>
        /// ///////////////////////////////////////////////////////////////////
        /// </summary>
        /// <param name="validationSet"></param>
        /// <returns></returns>
        public async Task<Stream> DownloadPackageFileToDiskAsync(PackageValidationSet validationSet)
        {
            var packageUri = await GetPackageReadUriAsync(validationSet);

            return await _packageDownloader.DownloadAsync(packageUri, CancellationToken.None);
        }

        public Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet,
                _pathTemplate,
                _extension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public async Task BackupPackageFileFromValidationSetPackageAsync(PackageValidationSet validationSet)
        {
            _logger.LogInformation(
                "Backing up package for validation set {ValidationTrackingId} ({PackageId} {PackageVersion}).",
                validationSet.ValidationTrackingId,
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion);

            var packageUri = await GetPackageForValidationSetReadUriAsync(
                validationSet,
                DateTimeOffset.UtcNow.Add(AccessDuration));

            using (var packageStream = await _packageDownloader.DownloadAsync(packageUri, CancellationToken.None))
            {
                await StorePackageFileInBackupLocationAsync(validationSet, packageStream);
            }
        }

        public Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet,
                _pathTemplate,
                _extension);

            return CopyFileAsync(
                _folderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildFileName(
                validationSet,
                _pathTemplate,
                _extension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                fileName,
                _folderName,
                fileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }

        public Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
        {
            var srcFileName = BuildValidationSetPackageFileName(validationSet);

            var destFileName = BuildFileName(
                validationSet,
                _pathTemplate,
                _extension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                _folderName,
                destFileName,
                destAccessCondition);
        }

        public Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            return _fileStorageService.FileExistsAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Deleting package for validation set {ValidationTrackingId} from {FolderName}/{FileName}.",
                validationSet.ValidationTrackingId,
                CoreConstants.ValidationFolderName,
                fileName);

            return _fileStorageService.DeleteFileAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, DateTimeOffset endOfAccess)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            return _fileStorageService.GetFileReadUriAsync(CoreConstants.ValidationFolderName, fileName, endOfAccess);
        }

        public Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl)
        {
            var destFileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Copying URL {SrcPackageUrl} to {DestFolderName}/{DestFileName}.",
                srcPackageUrl,
                CoreConstants.ValidationFolderName,
                srcPackageUrl);

            return _fileStorageService.CopyFileAsync(
                new Uri(srcPackageUrl),
                CoreConstants.ValidationFolderName,
                destFileName,
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        private Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
        {
            _logger.LogInformation(
                "Copying file {SrcFolderName}/{SrcFileName} to {DestFolderName}/{DestFileName}.",
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName);

            return _fileStorageService.CopyFileAsync(
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName,
                destAccessCondition);
        }

        private string BuildValidationSetPackageFileName(PackageValidationSet validationSet)
        {
            return $"validation-sets/{validationSet.ValidationTrackingId}/" +
                $"{validationSet.PackageId.ToLowerInvariant()}." +
                $"{validationSet.PackageNormalizedVersion.ToLowerInvariant()}" +
                _extension;
        }
    }

}
