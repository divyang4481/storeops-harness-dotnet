<#
.SYNOPSIS
  Runs StoreOps locally WITHOUT Docker (Release build, Kestrel on http://localhost:5080).
  Leave this window open; in a second window run:  powershell -ExecutionPolicy Bypass -File scripts\demo.ps1
#>
param([int]$Port = 5080)
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet build StoreOps.sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet src\StoreOps.Api\bin\Release\net8.0\StoreOps.Api.dll --urls "http://localhost:$Port"
