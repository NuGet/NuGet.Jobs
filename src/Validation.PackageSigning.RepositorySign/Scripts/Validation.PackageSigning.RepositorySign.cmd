@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.validation.packagesigning.repositorysign.Title}"

title #{Jobs.validation.packagesigning.repositorysign.Title}

start /w Validation.PackageSigning.RepositorySign.exe ^
    -Configuration #{Jobs.validation.packagesigning.repositorysign.Configuration} ^
    -InstrumentationKey "#{Jobs.validation.packagesigning.repositorysign.InstrumentationKey}"

echo "Finished #{Jobs.validation.packagesigning.repositorysign.Title}"

goto Top
