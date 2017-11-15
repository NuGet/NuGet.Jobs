@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}"
	
	title #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}

	start /w NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -InstrumentationKey "#{Jobs.validation.packagesigning.extractandvalidatesignature.InstrumentationKey}" -verbose true -Interval #{Jobs.validation.packagesigning.extractandvalidatesignature.Interval}

	echo "Finished #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}"

	goto Top