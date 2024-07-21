[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$JobsAssemblyVersion = '4.3.0',
    [string]$JobsPackageVersion = '4.3.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranchCommit = '5f2e842d25841ec1f11b8113151d11f38e550a55' #DevSkim: ignore DS173237. Not a secret/token. It is a commit hash.
)

# For TeamCity - If any issue occurs, this script fails the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

if (-not (Test-Path "$PSScriptRoot/build")) {
    New-Item -Path "$PSScriptRoot/build" -ItemType "directory"
}

Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/NuGet/ServerCommon/$BuildBranchCommit/build/init.ps1" -OutFile "$PSScriptRoot/build/init.ps1"
. "$PSScriptRoot/build/init.ps1" -BuildBranchCommit "$BuildBranchCommit"

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Cleaning test results' { Clear-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors
    
Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' { `
        $JobsAssemblyInfo =
            "src\Catalog\Properties\AssemblyInfo.g.cs",
            "src\CopyAzureContainer\Properties\AssemblyInfo.g.cs",
            "src\Gallery.CredentialExpiration\Properties\AssemblyInfo.g.cs",
            "src\Microsoft.PackageManagement.Search.Web\Properties\AssemblyInfo.g.cs",
            "src\Ng\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.Auxiliary2AzureSearch\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.Catalog2AzureSearch\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.Catalog2Registration\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.Common\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.Db2AzureSearch\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.GitHubIndexer\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Protocol.Catalog\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.AzureSearch\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Metadata.Catalog.Monitoring\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Revalidate\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.SearchService.Core\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.V3\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Validation.Orchestrator\Properties\AssemblyInfo.g.cs",
            "src\NuGet.SupportRequests.Notifications\Properties\AssemblyInfo.g.cs",
            "src\NuGetCDNRedirect\Properties\AssemblyInfo.g.cs",
            "src\PackageHash\Properties\AssemblyInfo.g.cs",
            "src\PackageLagMonitor\Properties\AssemblyInfo.g.cs",
            "src\SplitLargeFiles\Properties\AssemblyInfo.g.cs",
            "src\Stats.AzureCdnLogs.Common\Properties\AssemblyInfo.g.cs",
            "src\Stats.CDNLogsSanitizer\Properties\AssemblyInfo.g.cs",
            "src\Stats.CollectAzureChinaCDNLogs\Properties\AssemblyInfo.g.cs",
            "src\Stats.LogInterpretation\Properties\AssemblyInfo.g.cs",
            "src\Stats.PostProcessReports\Properties\AssemblyInfo.g.cs",
            "src\Stats.Warehouse\Properties\AssemblyInfo.g.cs",
            "src\StatusAggregator\Properties\AssemblyInfo.g.cs",
            "src\Validation.Common.Job\Properties\AssemblyInfo.g.cs",
            "src\Validation.ContentScan.Core\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.Core\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.ProcessSignature\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.RevalidateCertificate\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.ValidateCertificate\Properties\AssemblyInfo.g.cs",
            "src\Validation.ScanAndSign.Core\Properties\AssemblyInfo.g.cs",
            "src\Validation.Symbols.Core\Properties\AssemblyInfo.g.cs",
            "src\Validation.Symbols\Properties\AssemblyInfo.g.cs"
            
        $JobsAssemblyInfo | ForEach-Object {
            Set-VersionInfo (Join-Path $PSScriptRoot $_) -AssemblyVersion $JobsAssemblyVersion -PackageVersion $JobsPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
    Install-SolutionPackages -path (Join-Path $PSScriptRoot "packages.config") -output (Join-Path $PSScriptRoot "packages") -ExcludeVersion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Removing .editorconfig file' { Remove-EditorconfigFile -Directory $PSScriptRoot } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
    param($Configuration, $BuildNumber, $SolutionPath, $SkipRestore)
    Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $SolutionPath -SkipRestore:$SkipRestore `
    } `
    -args $Configuration, $BuildNumber, (Join-Path $PSScriptRoot "NuGet.Jobs.sln"), $SkipRestore `
    -ev +BuildErrors 

Invoke-BuildStep 'Building functional test solution' { 
        $SolutionPath = Join-Path $PSScriptRoot "tests\NuGetServicesMetadata.FunctionalTests.sln"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $SolutionPath -SkipRestore:$SkipRestore `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Creating artifacts' {
        $JobsProjects =
            "src\Catalog\NuGet.Services.Metadata.Catalog.csproj",
            "src\Microsoft.PackageManagement.Search.Web\Microsoft.PackageManagement.Search.Web.csproj",
            "src\NuGet.Jobs.Common\NuGet.Jobs.Common.csproj",
            "src\NuGet.Protocol.Catalog\NuGet.Protocol.Catalog.csproj",
            "src\NuGet.Services.AzureSearch\NuGet.Services.AzureSearch.csproj",
            "src\NuGet.Services.Metadata.Catalog.Monitoring\NuGet.Services.Metadata.Catalog.Monitoring.csproj",
            "src\NuGet.Services.V3\NuGet.Services.V3.csproj",
            "src\Stats.LogInterpretation\Stats.LogInterpretation.csproj",
            "src\Validation.Common.Job\Validation.Common.Job.csproj",
            "src\Validation.ContentScan.Core\Validation.ContentScan.Core.csproj",
            "src\Validation.ScanAndSign.Core\Validation.ScanAndSign.Core.csproj",
            "src\Validation.Symbols.Core\Validation.Symbols.Core.csproj"
        $JobsProjects | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $JobsPackageVersion -Branch $Branch -Symbols
        }

        $JobsNuspecProjects =
            "src\ArchivePackages\ArchivePackages.csproj",
            "src\CopyAzureContainer\CopyAzureContainer.csproj",
            "src\Gallery.CredentialExpiration\Gallery.CredentialExpiration.csproj",
            "src\Gallery.Maintenance\Gallery.Maintenance.nuspec",
            "src\Ng\Catalog2Dnx.nuspec",
            "src\Ng\Catalog2icon.nuspec",
            "src\Ng\Catalog2Monitoring.nuspec",
            "src\Ng\Db2Catalog.nuspec",
            "src\Ng\Db2Monitoring.nuspec",
            "src\Ng\Monitoring2Monitoring.nuspec",
            "src\Ng\MonitoringProcessor.nuspec",
            "src\Ng\Ng.Operations.nuspec",
            "src\NuGet.Jobs.Auxiliary2AzureSearch\NuGet.Jobs.Auxiliary2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2AzureSearch\NuGet.Jobs.Catalog2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2Registration\NuGet.Jobs.Catalog2Registration.nuspec",
            "src\NuGet.Jobs.Db2AzureSearch\NuGet.Jobs.Db2AzureSearch.nuspec",
            "src\NuGet.Jobs.GitHubIndexer\NuGet.Jobs.GitHubIndexer.nuspec",
            "src\NuGet.Services.Revalidate\NuGet.Services.Revalidate.csproj",
            "src\NuGet.Services.Validation.Orchestrator\Validation.Orchestrator.nuspec",
            "src\NuGet.Services.Validation.Orchestrator\Validation.SymbolsOrchestrator.nuspec",
            "src\NuGet.SupportRequests.Notifications\NuGet.SupportRequests.Notifications.csproj",
            "src\PackageLagMonitor\Monitoring.PackageLag.csproj",
            "src\SplitLargeFiles\SplitLargeFiles.nuspec",
            "src\Stats.AggregateCdnDownloadsInGallery\Stats.AggregateCdnDownloadsInGallery.csproj",
            "src\Stats.CDNLogsSanitizer\Stats.CDNLogsSanitizer.csproj",
            "src\Stats.CollectAzureCdnLogs\Stats.CollectAzureCdnLogs.csproj",
            "src\Stats.CollectAzureChinaCDNLogs\Stats.CollectAzureChinaCDNLogs.csproj",
            "src\Stats.CreateAzureCdnWarehouseReports\Stats.CreateAzureCdnWarehouseReports.csproj",
            "src\Stats.ImportAzureCdnStatistics\Stats.ImportAzureCdnStatistics.csproj",
            "src\Stats.PostProcessReports\Stats.PostProcessReports.nuspec",
            "src\Stats.RollUpDownloadFacts\Stats.RollUpDownloadFacts.csproj",
            "src\StatusAggregator\StatusAggregator.csproj",
            "src\Validation.PackageSigning.ProcessSignature\Validation.PackageSigning.ProcessSignature.csproj",
            "src\Validation.PackageSigning.RevalidateCertificate\Validation.PackageSigning.RevalidateCertificate.csproj",
            "src\Validation.PackageSigning.ValidateCertificate\Validation.PackageSigning.ValidateCertificate.csproj",
            "src\Validation.Symbols.Core\Validation.Symbols.Core.csproj",
            "src\Validation.Symbols\Validation.Symbols.Job.csproj"
        $JobsNuspecProjects | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $JobsPackageVersion -Branch $Branch
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber `
    } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
