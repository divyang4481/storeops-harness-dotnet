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

# --- Feature 01: shift handover bulk update (PATCH /api/activities/bulk-status) ---
$t1 = Call 'Create task 1 for handover' POST '/activities' 'S-1002' '{"title":"Price check aisle 2","category":"General"}'
$t2 = Call 'Create task 2 for handover' POST '/activities' 'S-1002' '{"title":"Planogram reset bay 7","category":"Planogram"}'
$missing = [guid]::NewGuid()
$bulk = @"
{"items":[{"id":"$($t1.id)","status":"Done"},{"id":"$($t2.id)","status":"Blocked"},{"id":"$missing","status":"Done"}],
 "incomingStaffId":"S-1001","note":"Evening shift handover"}
"@
Call 'Bulk handover: 2 updated, 1 rejected (NOT_FOUND) - partial failure' PATCH '/activities/bulk-status' 'S-1002' $bulk | Out-Null
Call 'Incoming store manager receives SHIFT_HANDOVER alert' GET '/alerts?type=ShiftHandover' 'S-1001' | Out-Null

# --- Feature 02: SLA breach alerting (background sweep; run-local uses a 5 s sweep and 0 min grace for the demo) ---
$due = (Get-Date).ToUniversalTime().AddMinutes(-5).ToString('o')
$sla = Call 'Create HIGH Grocery task already past due' POST '/activities' 'S-1002' "{`"title`":`"Chilled delivery check`",`"priority`":`"High`",`"department`":`"Grocery`",`"dueAt`":`"$due`"}"
Write-Host ''; Write-Host 'Waiting 15 s for the SLA sweep...' -ForegroundColor DarkGray; Start-Sleep -Seconds 15
Call 'Department lead (Grocery) receives SLA_BREACH alert' GET '/alerts?type=SlaBreach' 'S-1002' | Out-Null
Call 'Store manager receives SLA escalation after grace period' GET '/alerts?type=Escalation' 'S-1001' | Out-Null
