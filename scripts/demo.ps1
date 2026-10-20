<#
.SYNOPSIS
  Demonstration calls against a running StoreOps (local or AWS). Prints each request and response for the
  DEPLOYMENT.md screenshots. Feature sections are appended as harness runs add endpoints.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\demo.ps1                                   # local (scripts\run-local.ps1)
  powershell -ExecutionPolicy Bypass -File scripts\demo.ps1 -BaseUrl http://storeops-dev.xxxx.ap-south-1.elasticbeanstalk.com
#>
param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$api = "$($BaseUrl.TrimEnd('/'))/api"
function Call([string]$Title, [string]$Method, [string]$Path, [string]$StaffId, [string]$Body = $null) {
    Write-Host ""
    Write-Host "=== $Title" -ForegroundColor Cyan
    Write-Host "$Method $api$Path   (X-Staff-Id: $StaffId)" -ForegroundColor DarkGray
    if ($Body) { Write-Host $Body -ForegroundColor DarkGray }
    $params = @{ Uri = "$api$Path"; Method = $Method; Headers = @{ 'X-Staff-Id' = $StaffId }; ContentType = 'application/json' }
    if ($Body) { $params.Body = $Body }
    try {
        $result = Invoke-RestMethod @params
        Write-Host ($result | ConvertTo-Json -Depth 6)
        return $result
    } catch {
        $code = [int]$_.Exception.Response.StatusCode
        Write-Host "HTTP $code $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        return $null
    }
}

Write-Host "StoreOps demo against $BaseUrl" -ForegroundColor Green

# --- Base API (9 endpoints) ---
$a = Call 'Create activity (dept lead, ST-001)' POST '/activities' 'S-1002' '{"title":"Restock aisle 4","category":"Restocking","priority":"High","assigneeStaffId":"S-1003"}'
Call 'Block it -> escalation alert to store manager via event bus' PATCH "/activities/$($a.id)" 'S-1002' '{"status":"Blocked"}' | Out-Null
Call 'Store manager alerts' GET '/alerts?type=Escalation' 'S-1001' | Out-Null
Call 'Undefined status value is rejected (400)' PATCH "/activities/$($a.id)" 'S-1002' '{"status":7}' | Out-Null
Call 'No caller header -> 401' GET '/activities' '' | Out-Null
