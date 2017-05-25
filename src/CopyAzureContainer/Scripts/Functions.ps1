Function Install-AzCopy
{
    Param ( [string]$toolsPath="$PSScriptRoot\bin\tools\azcopy")

    $toolsPath
    $azcopy = "$toolsPath\azcopy.exe"

    if(Test-Path $azcopy)
    {
        Write-Output "AzCopy.exe has already been downloaded." 
    } 
    else 
    {
        $bootstrap = "$env:TEMP\azcopy_"+[System.Guid]::NewGuid()
        $output = "$bootstrap\extracted"
        $msi = "$bootstrap\MicrosoftAzureStorageTools.msi"

        Write-Output "Downloading AzCopy."
        Write-Output "Bootstrap directory: '$bootstrap'"

        mkdir $toolsPath -ErrorAction Ignore | Out-Null
        mkdir $bootstrap | Out-Null

        Invoke-WebRequest -Uri "http://aka.ms/downloadazcopy" -OutFile $msi
        Unblock-File $msi

        Write-Host "Extracting AzCopy"
        Start-Process msiexec -Argument "/a $msi /qb TARGETDIR=$output /quiet" -Wait

        Copy-Item "$output\Microsoft SDKs\Azure\AzCopy\*" $toolsPath -Force
        Remove-Item $bootstrap -Recurse -Force
    }
}

Function Uninstall-NuGetService() {
	Param ([string]$ServiceName)

	if (Get-Service $ServiceName -ErrorAction SilentlyContinue)
	{
		Write-Host Removing service $ServiceName...
		Stop-Service $ServiceName -Force
		sc.exe delete $ServiceName 
		Write-Host Removed service $ServiceName.
	} else {
		Write-Host Skipping removal of service $ServiceName - no such service exists.
	}
}

Function Install-NuGetService() {
	Param ([string]$ServiceName, [string]$ServiceTitle, [string]$ScriptToRun)

	Write-Host Installing service $ServiceName...

	$installService = "nssm install $ServiceName $ScriptToRun"
	cmd /C $installService
	
	Set-Service -Name $ServiceName -DisplayName "$ServiceTitle - $ServiceName" -Description "Runs $ServiceTitle." -StartupType Automatic
	sc.exe failure $ServiceName reset= 30 actions= restart/5000 

	# Run service
	net start $ServiceName
		
	Write-Host Installed service $ServiceName.
}