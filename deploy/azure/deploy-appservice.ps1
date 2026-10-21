<#
.SYNOPSIS
  Deploys StoreOps to Azure App Service (Linux, built-in .NET 8 runtime) with ZIP deploy — no Docker, no registry.
  First run creates resource group, Linux plan and web app; later runs only redeploy. Ends with demo.ps1 against the URL.
.PREREQUISITES
  Azure CLI (az login) with rights to create a resource group / App Service in YOUR subscription; .NET 8 SDK.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\azure\deploy-appservice.ps1 -AppName storeops-<yourinitials>
  powershell -ExecutionPolicy Bypass -File deploy\azure\deploy-appservice.ps1 -BundleOnly   # just build artifacts\storeops-azure.zip (portal upload)
#>
param(
    [string]$AppName = 'storeops-demo-' + (Get-Random -Maximum 99999),   # must be globally unique (becomes <name>.azurewebsites.net)
    [string]$ResourceGroup = 'rg-storeops-demo',
    [string]$Location = 'centralindia',
    [string]$PlanName = 'plan-storeops-demo',
    [string]$Sku = 'B1',                 # B1 supports Always On, which keeps the SLA background sweep running
    [int]$SlaSweepSeconds = 10,
    [int]$SlaGraceMinutes = 0,
    [switch]$BundleOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

# 1. Bundle: runtime-dependent publish output, zipped (App Service runs StoreOps.Api.dll on its .NET 8 image)
$publish = Join-Path $root 'artifacts\publish-azure'
$zip = Join-Path $root 'artifacts\storeops-azure.zip'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
dotnet publish src\StoreOps.Api\StoreOps.Api.csproj -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
Write-Host "Bundle: $zip" -ForegroundColor Green
if ($BundleOnly) { return }

function Az { & az @args; if ($LASTEXITCODE -ne 0) { throw "az $($args -join ' ') failed" } }

# 2. Infrastructure (idempotent)
Az group create --name $ResourceGroup --location $Location --output none
Az appservice plan create --name $PlanName --resource-group $ResourceGroup --is-linux --sku $Sku --output none
& az webapp show --name $AppName --resource-group $ResourceGroup --output none 2>$null
if ($LASTEXITCODE -ne 0) {
    Az webapp create --name $AppName --resource-group $ResourceGroup --plan $PlanName --runtime 'DOTNETCORE:8.0' --output none
}
# Single instance on purpose: state is in memory (see docs/DEPLOYMENT-AZURE.md)
Az webapp config set --name $AppName --resource-group $ResourceGroup --always-on true --http20-enabled true --min-tls-version 1.2 --output none
Az webapp update --name $AppName --resource-group $ResourceGroup --https-only true --output none
Az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --output none --settings `
    ASPNETCORE_ENVIRONMENT=Production "Sla__SweepIntervalSeconds=$SlaSweepSeconds" "Sla__EscalationGracePeriodMinutes=$SlaGraceMinutes"

# 3. ZIP deploy
Az webapp deploy --name $AppName --resource-group $ResourceGroup --src-path $zip --type zip --output none

# 4. Live URL + demo
$hostName = (& az webapp show --name $AppName --resource-group $ResourceGroup --query defaultHostName --output tsv)
$url = "https://$hostName"
Write-Host "Deployed: $url" -ForegroundColor Green
for ($i = 0; $i -lt 30; $i++) {
    try { Invoke-WebRequest "$url/api/alerts" -UseBasicParsing -Headers @{ 'X-Staff-Id' = 'S-1001' } | Out-Null; break } catch { Start-Sleep -Seconds 5 }
}
powershell -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\demo.ps1') -BaseUrl $url
