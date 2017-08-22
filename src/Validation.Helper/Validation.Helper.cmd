@echo off

if "%1"=="" (
    echo Usage:
    echo 	%~nx0 -Action Rescan -PackageId ^<package id^> -PackageVersion ^<package version^>
    echo 	%~nx0 -Action MarkClean -PackageId ^<package id^> -PackageVersion ^<package version^> -ValidationId ^<validation Id ^(GUID^)^> -Alias ^<your alias^> -Comment ^<comment^>
    exit 1
)

SET vn=#{Deployment.Azure.KeyVault.VaultName}
SET clientid=#{Deployment.Azure.KeyVault.ClientId}
SET tp=#{Deployment.Azure.KeyVault.CertificateThumbprint}
SET la=#{Jobs.validation.DataStorageAccount}
SET dsa=#{Jobs.validation.DataStorageAccount}
SET cn=#{Jobs.validation.ContainerName}
SET ik=#{Jobs.validation.VcsValidatorInstrumentationKey}
SET gba=#{Jobs.validation.GalleryBaseAddress}

NuGet.Jobs.Validation.Helper.exe -VaultName "%vn%" -ClientId "%clientid%" -CertificateThumbprint "%tp%" -DataStorageAccount "%dsa%" -ContainerName "%cn%" -InstrumentationKey "%ik%" -GalleryBaseAddress "%gba%" %*

