<#
.SYNOPSIS
  Deletes the StoreOps demo resource group (web app + plan) to stop Azure charges.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\azure\teardown-appservice.ps1
#>
param([string]$ResourceGroup = 'rg-storeops-demo')
az group delete --name $ResourceGroup --yes --no-wait
Write-Host "Deletion of $ResourceGroup started (runs in the background in Azure)."
