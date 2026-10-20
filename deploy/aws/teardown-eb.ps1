<#
.SYNOPSIS
  Terminates the StoreOps Elastic Beanstalk environment (stops EC2 charges). Keeps the application and versions.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\aws\teardown-eb.ps1 -Region ap-south-1
#>
param([string]$Region = 'ap-south-1', [string]$EnvName = 'storeops-dev')
aws elasticbeanstalk terminate-environment --environment-name $EnvName --region $Region
aws elasticbeanstalk wait environment-terminated --environment-names $EnvName --region $Region
Write-Host "Terminated $EnvName"
