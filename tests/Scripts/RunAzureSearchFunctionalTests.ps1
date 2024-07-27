[CmdletBinding()]
param(
    [string]$Config = "Release",
    [string]$SolutionPath = "NuGet.Jobs.FunctionalTests.sln"
)

# Move working directory one level up
$root = (Get-Item $PSScriptRoot).parent
$rootName = $root.FullName
$rootRootName = $root.parent.FullName

# Required tools
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VsInstallationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
$msBuild = Join-Path $VsInstallationPath "MSBuild\Current\Bin\msbuild"
$xunit = "$rootRootName\packages\xunit.runner.console\tools\net472\xunit.console.exe"
$nuget = "$rootName\nuget.exe"
& "$rootName\Scripts\DownloadLatestNuGetExeRelease.ps1" $rootName

Write-Host "Restoring solution tools"
& $nuget install (Join-Path $PSScriptRoot "..\..\packages.config") -SolutionDirectory (Join-Path $PSScriptRoot "..\..") -NonInteractive -ExcludeVersion

# Test results files
$functionalTestsResults = "$rootRootName/functionaltests.*.xml"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore

# Restore packages
Write-Host "Restoring solution"
$fullSolutionPath = "$rootName\$SolutionPath"
& $nuget "restore" $fullSolutionPath "-NonInteractive"
if ($LastExitCode) {
    throw "Failed to restore packages!"
}

# Build the solution
Write-Host "Building solution"
& $msBuild $fullSolutionPath "/p:Configuration=$Config" "/p:Platform=Any CPU" "/m" "/v:M" "/fl" "/nr:false"
if ($LastExitCode) {
    throw "Failed to build solution!"
}

# Run functional tests
$exitCode = 0

Write-Host "Running Azure Search functional tests..."
& $xunit "NuGet.Services.AzureSearch.FunctionalTests\bin\$Config\net472\NuGet.Services.AzureSearch.FunctionalTests.dll" -xml "$rootRootName\functionaltests.AzureSearchTests.xml"
if ($LastExitCode) {
    $exitCode = 1
}

exit $exitCode
