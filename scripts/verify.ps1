<#
.SYNOPSIS
  One-command quality gate for StoreOps: SDK check -> restore -> lint -> build -> test -> live smoke test.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\verify.ps1
  powershell -ExecutionPolicy Bypass -File scripts\verify.ps1 -SkipSmoke
#>
param(
    [switch]$SkipSmoke,
    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Invoke-Step 'SDK check (needs .NET 8 SDK or newer)' { dotnet --list-sdks; dotnet --version }
Invoke-Step 'Restore' { dotnet restore StoreOps.sln }
Invoke-Step 'Lint: dotnet format --verify-no-changes' { dotnet format StoreOps.sln --verify-no-changes --no-restore }
Invoke-Step 'Build: Release, warnings as errors, analyzers + StyleCop' { dotnet build StoreOps.sln -c Release --no-restore }
Invoke-Step 'Test: xUnit + WebApplicationFactory' { dotnet test StoreOps.sln -c Release --no-build --logger "console;verbosity=normal" }

if (-not $SkipSmoke) {
    Write-Host ""
    Write-Host "==> Smoke test: run API on http://127.0.0.1:$Port and call endpoints" -ForegroundColor Cyan
    $dll = Join-Path (Get-Location) 'src\StoreOps.Api\bin\Release\net8.0\StoreOps.Api.dll'
    $proc = Start-Process dotnet -ArgumentList "`"$dll`" --urls http://127.0.0.1:$Port" -PassThru -WindowStyle Hidden
    try {
        $base = "http://127.0.0.1:$Port/api"
        $ready = $false
        for ($i = 0; $i -lt 40 -and -not $ready; $i++) {
            try { Invoke-RestMethod "$base/alerts" | Out-Null; $ready = $true } catch { Start-Sleep -Milliseconds 500 }
        }
        if (-not $ready) { throw "API did not start on port $Port" }

        $activity = Invoke-RestMethod "$base/activities" -Method Post -ContentType 'application/json' `
            -Body '{"storeId":"ST-001","title":"Smoke stock count","assigneeStaffId":"S-1001"}'
        Invoke-RestMethod "$base/activities/$($activity.id)" -Method Patch -ContentType 'application/json' -Body '{"status":"Cancelled"}' | Out-Null
        $programme = Invoke-RestMethod "$base/programmes" -Method Post -ContentType 'application/json' -Body '{"name":"Smoke programme"}'
        Invoke-RestMethod "$base/programmes/$($programme.id)/members" -Method Post -ContentType 'application/json' -Body '{"staffId":"S-2001"}' | Out-Null
        $alerts = @(Invoke-RestMethod "$base/alerts?storeId=ST-001")
        if ($alerts.Count -lt 1) { throw "Expected an ACTIVITY_CANCELLED alert raised via the event bus" }

        # Works on Windows PowerShell 5.1 (WebException) and PowerShell 7 (HttpResponseException).
        $status = 0
        try { Invoke-RestMethod "$base/activities/$([guid]::NewGuid())" | Out-Null } catch { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -ne 404) { throw "Expected 404 for unknown activity, got $status" }
        Write-Host "Smoke test passed ($($alerts.Count) alert(s) for ST-001)" -ForegroundColor Green
    }
    finally {
        if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    }
}

Write-Host ""
Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
