
// ToDo Be Moved in GalleryCore
using NuGetGallery;

namespace Validation.Symbols
{
    public class SymbolPackageFileMetadataService : IFileMetadataService
    {
        public string FileFolderName => "symbol-packages";

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => ".snupkg";

        public string ValidationFolderName => CoreConstants.ValidationFolderName;

        public string FileBackupsFolderName => "symbol-package-backups";

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
