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
            try { Invoke-WebRequest "$base/alerts" -UseBasicParsing -Headers @{ 'X-Staff-Id' = 'S-1001' } | Out-Null; $ready = $true } catch { Start-Sleep -Milliseconds 500 }
        }
        if (-not $ready) { throw "API did not start on port $Port" }

        # Caller identity: ST-001 department lead creates and blocks a task; the store manager must receive an ESCALATION alert.
        $lead = @{ 'X-Staff-Id' = 'S-1002' }
        $manager = @{ 'X-Staff-Id' = 'S-1001' }
        $activity = Invoke-RestMethod "$base/activities" -Method Post -Headers $lead -ContentType 'application/json' `
            -Body '{"title":"Smoke stock count","category":"Restocking","assigneeStaffId":"S-1003"}'
        Invoke-RestMethod "$base/activities/$($activity.id)" -Method Patch -Headers $lead -ContentType 'application/json' -Body '{"status":"Blocked"}' | Out-Null
        $programme = Invoke-RestMethod "$base/programmes" -Method Post -Headers $manager -ContentType 'application/json' -Body '{"name":"Smoke programme"}'
        Invoke-RestMethod "$base/programmes/$($programme.id)/members" -Method Post -Headers $manager -ContentType 'application/json' -Body '{"staffId":"S-1003"}' | Out-Null
        $alerts = @(Invoke-RestMethod "$base/alerts?type=Escalation" -Headers $manager)
        if (-not ($alerts | Where-Object { $_.sourceReference -eq $activity.id })) { throw "Expected an ESCALATION alert for S-1001 raised via the event bus" }

        # Works on Windows PowerShell 5.1 (WebException) and PowerShell 7 (HttpResponseException).
        $status = 0
        try { Invoke-RestMethod "$base/activities/$([guid]::NewGuid())" -Headers $manager | Out-Null } catch { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -ne 404) { throw "Expected 404 for unknown activity, got $status" }
        Write-Host "Smoke test passed (escalation alert delivered to S-1001)" -ForegroundColor Green
    }
    finally {
        if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    }
}

Write-Host ""
Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
