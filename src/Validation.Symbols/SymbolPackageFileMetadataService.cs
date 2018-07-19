
// ToDo Be Moved in GalleryCore
using NuGetGallery;

namespace Validation.Symbols
{
    public class SymbolPackageFileMetadataService : IFileMetadataService
    {
        public string FileFolderName => "symbols";

        public string FileSavePathTemplate => CoreConstants.PackageFileSavePathTemplate;

        public string FileExtension => ".snupkg";

        public string ValidationFolderName => CoreConstants.ValidationFolderName;

        public string FileBackupsFolderName => "symbol-backups";

        public string FileBackupSavePathTemplate => CoreConstants.PackageFileBackupSavePathTemplate;
    }
}
