@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}"
	
	title #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}

	start /w NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature.exe -Configuration #{Jobs.validation.configuration} -InstrumentationKey "#{Jobs.validation.packagesigning.extractandvalidatesignature.InstrumentationKey}"

	echo "Finished #{Jobs.validation.packagesigning.extractandvalidatesignature.Title}"

	goto Top