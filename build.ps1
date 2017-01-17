[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
	[ValidateSet("Release","rtm", "rc", "beta", "beta2", "final", "xprivate", "zlocal")]
    [string]$ReleaseLabel = 'zlocal',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
	[string]$SimpleVersion = '1.0.0',
	[string]$SemanticVersion = '1.0.0-zlocal',
	[string]$Branch,
	[string]$CommitSHA
)

# For TeamCity - If any issue occurs, this script fail the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

Function Clean-Tests {
	[CmdletBinding()]
	param()
	
	Trace-Log 'Cleaning test results'
	
	Remove-Item (Join-Path $PSScriptRoot "Results.*.xml")
}

Function Prepare-Vcs-Callback {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Preparing Validation.Callback.Vcs Package'
    
    $ZipPackagePath = "src\Validation.Callback.Vcs\obj\Validation.Callback.Vcs.zip"
    
    if (Test-Path $ZipPackagePath) {
        Remove-Item $ZipPackagePath
    }
    
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "14" "src\Validation.Callback.Vcs\Validation.Callback.Vcs.csproj" -Target "Package" -MSBuildProperties "/P:PackageLocation=obj\Validation.Callback.Vcs.zip" -SkipRestore
}

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()
	
Invoke-BuildStep 'Cleaning test results' { Clean-Tests } `
	-ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors
	
Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors
	
Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors
	
Invoke-BuildStep 'Restoring solution packages' { `
	Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages") -ExcludeVersion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
	param($Configuration, $BuildNumber, $SolutionPath, $SkipRestore)
	Build-Solution $Configuration $BuildNumber -MSBuildVersion "14" $SolutionPath -SkipRestore:$SkipRestore `
	} `
	-args $Configuration, $BuildNumber, (Join-Path $PSScriptRoot "NuGet.Jobs.sln"), $SkipRestore `
    -ev +BuildErrors
	
Invoke-BuildStep 'Prepare Validation.Callback.Vcs Package' { Prepare-Vcs-Callback } `
	-ev +BuildErrors
	
Invoke-BuildStep 'Creating artifacts' {
		New-Package (Join-Path $PSScriptRoot "src/Stats.CollectAzureCdnLogs/Stats.CollectAzureCdnLogs.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Stats.AggregateCdnDownloadsInGallery/Stats.AggregateCdnDownloadsInGallery.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Stats.ImportAzureCdnStatistics/Stats.ImportAzureCdnStatistics.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Stats.CreateAzureCdnWarehouseReports/Stats.CreateAzureCdnWarehouseReports.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/UpdateLicenseReports/UpdateLicenseReports.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Gallery.CredentialExpiration/Gallery.CredentialExpiration.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/ArchivePackages/ArchivePackages.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Search.GenerateAuxiliaryData/Search.GenerateAuxiliaryData.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/HandlePackageEdits/HandlePackageEdits.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Stats.RollUpDownloadFacts/Stats.RollUpDownloadFacts.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Validation.Callback.Vcs/Validation.Callback.Vcs.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
		New-Package (Join-Path $PSScriptRoot "src/Validation.Runner/Validation.Runner.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion -Branch $Branch
	} `
	-ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)