[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber
)

trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()
$JobsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.sln"
$JobsProjects = Get-SolutionProjects $JobsSolution
$ExcludeTestProjects =
    "tests\Validation.PackageSigning.Helpers\Tests.ContextHelpers.csproj"

Invoke-BuildStep 'Cleaning test results' { Clear-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Running tests' {
        $JobsTestProjects = $JobsProjects `
            | Where-Object { $_.IsTest } `
            | Where-Object { $ExcludeTestProjects -notcontains $_.RelativePath }

        $TestCount = 0
        
        $JobsTestProjects | ForEach-Object {
            $TestResultFile = Join-Path $PSScriptRoot "Results.$TestCount.xml"
            Trace-Log "Testing $($_.Path)"
            dotnet test $_.Path --no-restore --no-build --configuration $Configuration "-l:trx;LogFileName=$TestResultFile"
            if (-not (Test-Path $TestResultFile)) {
                Write-Error "The test run failed to produce a result file";
                exit 1;
            }
            $TestCount++
        }
    } `
    -ev +TestErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Tests completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)