@echo off

if "%1"=="" (
	echo Usage:
	echo 	%~nx0 -Action Rescan -PackageId ^<package id^> -PackageVersion ^<package version^>
	echo 	%~nx0 -Action MarkClean -PackageId ^<package id^> -PackageVersion ^<package version^> -ValidationId ^<validation Id ^(GUID^)^> -Comment ^<comment^>
	echo 		please include your alias with the comment
	exit 1
)

cd bin

SET vn=#{KeyVault:VaultName}
SET clientid=#{KeyVault:ClientId}
SET tp=#{KeyVault:CertificateThumbprint}
SET la=#{Jobs.validation.DataStorageAccount}
SET dsa=#{Jobs.validation.DataStorageAccount}
SET cn=#{Jobs.validation.ContainerName}
SET ik=#{Jobs.validation.VcsValidatorInstrumentationKey}

NuGet.Jobs.Validation.Helper.exe -VaultName "%vn%" -ClientId "%clientid%" -CertificateThumbprint "%tp%" -LogsAzureStorageConnectionString "%la%" -DataStorageAccount "%dsa%" -ContainerName "%cn%" -InstrumentationKey "%ik%" %*

cd ..
