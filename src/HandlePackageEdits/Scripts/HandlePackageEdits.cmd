@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.handlepackageedits.Title}"

	title #{Jobs.handlepackageedits.Title}

    start /w handlepackageedits.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -PackageDatabase "#{Jobs.handlepackageedits.PackageDatabase}" -SourceStorage "#{Jobs.handlepackageedits.SourceStorage}" -BackupStorage "#{Jobs.handlepackageedits.BackupStorage}" -InstrumentationKey "#{Jobs.handlepackageedits.InstrumentationKey}" -Sleep #{Jobs.handlepackageedits.Sleep}

	echo "Finished #{Jobs.handlepackageedits.Title}"

	goto Top
	