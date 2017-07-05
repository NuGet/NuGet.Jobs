
function load_module($name)
{
    if (-not(Get-Module -Name $name))
    {
       $retVal = Get-Module -ListAvailable | where { $_.Name -eq $name }

        if ($retVal)
        {
            try
            {
                Import-Module $name -ErrorAction SilentlyContinue
                return $true
            }

            catch
            {
                $ErrorMessage = $_.Exception.Message
                Write-Host $ErrorMessage
                $retVal = $false
            }
        }
    }
    else
    {
        return $true
    }
}


$OctopusAzureModulePath = "C:\Program Files (x86)\Microsoft SDKs\Azure\PowerShell\ServiceManagement\Azure\Azure.psd1"
if(load_module $OctopusAzureModulePath)
{
    Write-Host "Imported Azure SDK PowerShell Module from $OctopusAzureModulePath" 
}
else
{
    Write-Host "Azure SDK PowerShell Module from $OctopusAzureModulePath already imported" 
}

Write-Host "Before getting subscriptions, clear folder %appdata%\Windows Azure Powershell\*"
$azureps = $env:APPDATA + '\Windows Azure Powershell\*'
Write-Host "Removing folder: " $azureps
rm $azureps
Write-Host "Removed appdata windows azure powershell folder"

$AzureCertificateThumbPrint = $OctopusParameters['Deployment.Azure.CertificateThumbprint']
$AzureSubscriptionName = $OctopusParameters['Deployment.Azure.SubscriptionName']
$AzureSubscriptionId = $OctopusParameters['Deployment.Azure.SubscriptionId']
$AzureWebsiteName = $OctopusParameters['Deployment.Azure.WebsiteName']
$WebPackageName = $OctopusParameters['Deployment.Azure.WebPackageName']
Write-Host "Web Package Name: " $WebPackageName
$WebPackagePath = $OctopusParameters['Octopus.Action.Package.InstallationDirectoryPath'] + '\' + $WebPackageName
Write-Host "Web Package Path: " $WebPackagePath

Write-Host "Looking for certificate in CurrentUser"
$cert = dir cert:\CurrentUser  -rec | where { $_.Thumbprint -eq $AzureCertificateThumbPrint } | Select -First 1
if(!$cert)
{
    Write-Host "Not Found in CurrentUser. Looking at LocalMachine"
    $cert = dir cert:\LocalMachine  -rec | where { $_.Thumbprint -eq $AzureCertificateThumbPrint } | Select -First 1
}

if(!$cert)
{
    throw "Certificate is not found in CurrentUser or LocalMachine"
}

Write-Host "Certificate was found. Setting azure subscription using the certificate..."
Set-AzureSubscription -SubscriptionName '$AzureSubscriptionName' -Certificate $cert -SubscriptionId $AzureSubscriptionId
Write-Host "Azure subscription was set successfully using the certificate obtained. Selecting default azure subscription..."
Select-AzureSubscription -SubscriptionName '$AzureSubscriptionName'
Write-Host "Current SubscriptionName" $AzureSubscriptionName
Write-Host "Selected default azure subscription. Publishing azure website..."
Publish-AzureWebsiteProject -Name $AzureWebsiteName -Package $WebPackagePath -Slot staging
Write-Host "Published azure website successfully."
