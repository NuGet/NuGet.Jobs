@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.createazurecdnwarehousereports.Title}"

	title #{Jobs.stats.createazurecdnwarehousereports.Title}

	start /w stats.createazurecdnwarehousereports.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -AzureCdnCloudStorageAccount "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageContainerName}" -StatisticsDatabase "#{Jobs.stats.createazurecdnwarehousereports.StatisticsDatabase}" -SourceDatabase "#{Jobs.stats.createazurecdnwarehousereports.SourceDatabase}" -DataStorageAccount "#{Jobs.stats.createazurecdnwarehousereports.DataStorageAccount}" -InstrumentationKey "#{Jobs.stats.createazurecdnwarehousereports.InstrumentationKey}" -DataContainerName "#{Jobs.stats.createazurecdnwarehousereports.DataContainerName}" -verbose true -Interval #{Jobs.stats.createazurecdnwarehousereports.Interval} 

	echo "Finished #{Jobs.stats.createazurecdnwarehousereports.Title}"

	goto Top
