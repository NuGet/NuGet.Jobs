@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.archivepackages.Title}"

	title #{Jobs.archivepackages.Title}

	start /w archivepackages.exe -Configuration "#{Jobs.archivepackages.Configuration}"

	echo "Finished #{Jobs.archivepackages.Title}"

	goto Top
	