@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)
set msbuild="%ProgramFiles(x86)%\MSBuild\14.0\bin\amd64\msbuild.exe"

REM Package restore
Powershell.exe -NoProfile -ExecutionPolicy ByPass -Command "& '%cd%\restoreNuGetExe.ps1'"
tools\nuget.exe restore NuGet.Jobs.sln -OutputDirectory %cd%\packages -NonInteractive -source "https://api.nuget.org/v3/index.json;https://www.myget.org/F/nugetbuild/api/v3/index.json"
if not "%errorlevel%"=="0" goto failure

REM Build Solution
%msbuild% NuGet.Jobs.sln /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false
if not "%errorlevel%"=="0" goto failure

REM Build VCS Callback Server
%msbuild% src/Validation.Callback.Vcs/Validation.Callback.Vcs.csproj /p:OutputPath=obj/"%config%" /t:Package /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false
if not "%errorlevel%"=="0" goto failure
echo "src\Validation.Callback.Vcs\obj\%config%\_PublishedWebsites\Validation.Callback.Vcs_Package\Validation.Callback.Vcs.zip"
echo "src\Validation.Callback.Vcs\obj\Validation.Callback.Vcs.zip"
copy "src\Validation.Callback.Vcs\obj\Debug\_PublishedWebsites\Validation.Callback.Vcs_Package\Validation.Callback.Vcs.zip" "src\Validation.Callback.Vcs\obj\Validation.Callback.Vcs.zip"
if not "%errorlevel%"=="0" goto failure

REM Test
tools\nuget.exe install xunit.runner.console -Version 2.0.0 -OutputDirectory packages
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.CollectAzureCdnLogs\bin\%config%\Tests.Stats.CollectAzureCdnLogs.dll
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.ImportAzureCdnStatistics\bin\%config%\Tests.Stats.ImportAzureCdnStatistics.dll
if not "%errorlevel%"=="0" goto failure

:success
exit 0

:failure
exit -1