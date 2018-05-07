@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.monitoring.packagelag.Title}"

title #{Jobs.monitoring.packagelag.Title}

start /w PackageLagMonitor.exe ^
    -Configuration #{Jobs.monitoring.packagelag.Configuration} ^
    -InstrumentationKey "#{Jobs.monitoring.packagelag.InstrumentationKey}"

echo "Finished #{Jobs.monitoring.packagelag.Title}"

goto Top
