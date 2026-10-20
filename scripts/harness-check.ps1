<#
.SYNOPSIS
  Deterministic hard-gate checks for the Evaluator agent. Runs ALL gates (does not stop at the first failure),
  writes .harness/output/checks.md and ends it with a machine-readable line:
      HARD_GATES: PASS            or      HARD_GATES: FAIL (HG-2, HG-5)
  Exit code 0 = all hard gates pass, 1 = at least one failed.
.PARAMETER BaseRef
  Commit the sprint started from (recorded in .harness/output/progress.md). Enables HG-8: every file changed since
  BaseRef under src/, tests/ or tools/ must be listed in .harness/output/generator-summary.md.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\harness-check.ps1 -BaseRef 84dda05
#>
param([string]$BaseRef = '')
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$out = Join-Path $root '.harness\output'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$covDir = Join-Path $out 'coverage'
if (Test-Path $covDir) { Remove-Item $covDir -Recurse -Force }

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Id, [string]$Name, [bool]$Pass, [string]$Evidence) {
    $results.Add([pscustomobject]@{ Id = $Id; Name = $Name; Result = $(if ($Pass) { 'PASS' } else { 'FAIL' }); Evidence = $Evidence })
    $colour = if ($Pass) { 'Green' } else { 'Red' }
    Write-Host ("{0} {1}: {2}" -f $Id, $Name, $(if ($Pass) { 'PASS' } else { 'FAIL' })) -ForegroundColor $colour
}
function Tail([string[]]$Lines, [int]$Count = 15) { (($Lines | Select-Object -Last $Count) -join "`n").Replace('|', '/') }

Write-Host '==> restore'; dotnet restore StoreOps.sln | Out-Null

# HG-1 Formatting / style (dotnet format)
$fmt = dotnet format StoreOps.sln --verify-no-changes --no-restore 2>&1
Add-Result 'HG-1' 'Format (dotnet format --verify-no-changes)' ($LASTEXITCODE -eq 0) $(if ($LASTEXITCODE -eq 0) { 'no changes needed' } else { Tail $fmt })

# HG-2 Build: Release, warnings as errors (nullable, .NET analyzers 8.0-recommended, IDE rules, StyleCop)
$build = dotnet build StoreOps.sln -c Release --no-restore 2>&1
$buildOk = $LASTEXITCODE -eq 0
$diag = $build | Select-String -Pattern ' (error|warning) [A-Z]+[0-9]+' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
Add-Result 'HG-2' 'Build, 0 warnings (analyzers + StyleCop + nullable)' $buildOk $(if ($buildOk) { '0 warnings, 0 errors' } else { Tail $diag 20 })

# HG-3 Full test suite with coverage collection
if ($buildOk) {
    $test = dotnet test StoreOps.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory $covDir 2>&1
    $testOk = $LASTEXITCODE -eq 0
    $summary = ($test | Select-String -Pattern '(Passed!|Failed!|Total tests|Failed )' | ForEach-Object { $_.Line.Trim() }) -join ' ; '
    Add-Result 'HG-3' 'All tests pass (xUnit + WebApplicationFactory)' $testOk $(if ($testOk) { $summary } else { Tail ($test | Select-String -Pattern 'Failed |Assert|Expected|Actual' | ForEach-Object { $_.Line.Trim() }) 25 })

    # HG-4 Architecture rules (module boundary, no cycles, event-bus-only injection, staff read-only, AppError contract)
    $arch = dotnet test tests\StoreOps.Api.Tests\StoreOps.Api.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~StoreOps.Api.Tests.Architecture' 2>&1
    Add-Result 'HG-4' 'Architecture tests (boundaries, cycles, event bus, staff read-only, AppError)' ($LASTEXITCODE -eq 0) (Tail ($arch | Select-String -Pattern 'Passed!|Failed|Cross-module|Raw exception|Circular|injects' | ForEach-Object { $_.Line.Trim() }) 15)

    # HG-5 Coverage thresholds per layer (service 80, routes 70, shared 60, overall 70)
    $covMd = Join-Path $out 'coverage.md'
    $gate = dotnet tools\StoreOps.CoverageGate\bin\Release\net8.0\StoreOps.CoverageGate.dll $covDir $covMd 2>&1
    Add-Result 'HG-5' 'Coverage thresholds (service 80 / routes 70 / shared 60 / overall 70)' ($LASTEXITCODE -eq 0) 'see coverage table below'
} else {
    Add-Result 'HG-3' 'All tests pass' $false 'skipped: build failed'
    Add-Result 'HG-4' 'Architecture tests' $false 'skipped: build failed'
    Add-Result 'HG-5' 'Coverage thresholds' $false 'skipped: build failed'
}

# HG-6 Error contract scan: no `throw new X` in Modules/ or Common/Http unless X is an AppError subtype (*Error)
$raw = Get-ChildItem src\StoreOps.Api\Modules, src\StoreOps.Api\Common\Http -Recurse -Filter *.cs |
    Select-String -Pattern 'throw\s+new\s+([A-Za-z_][A-Za-z0-9_]*)' |
    Where-Object { $_.Matches[0].Groups[1].Value -notmatch 'Error$' } |
    ForEach-Object { '{0}:{1}: {2}' -f (Resolve-Path -Relative $_.Path), $_.LineNumber, $_.Line.Trim() }
Add-Result 'HG-6' 'No raw exception throws in services/routes' (@($raw).Count -eq 0) $(if (@($raw).Count -eq 0) { '0 matches' } else { ($raw -join "`n").Replace('|', '/') })

# HG-7 Module boundary scan (dependency analyser): a module may only reference its own internals; cross-module types come from Contracts/
$cross = foreach ($module in (Get-ChildItem src\StoreOps.Api\Modules -Directory)) {
    Get-ChildItem $module.FullName -Recurse -Filter *.cs |
        Select-String -Pattern 'StoreOps\.Api\.Modules\.([A-Za-z]+)' -AllMatches |
        Where-Object { $_.Matches | Where-Object { $_.Groups[1].Value -ne $module.Name } } |
        ForEach-Object { '{0}:{1}: {2}' -f (Resolve-Path -Relative $_.Path), $_.LineNumber, $_.Line.Trim() }
}
Add-Result 'HG-7' 'No cross-module internal references (Contracts/ only)' (@($cross).Count -eq 0) $(if (@($cross).Count -eq 0) { '0 matches' } else { ($cross -join "`n").Replace('|', '/') })

# HG-8 Traceability: generator-summary.md lists every file changed in this sprint (only when -BaseRef is given)
if ($BaseRef) {
    $summaryPath = Join-Path $out 'generator-summary.md'
    $changed = @(git diff --name-only $BaseRef -- src tests tools) + @(git ls-files --others --exclude-standard -- src tests tools) |
        Where-Object { $_ } | Sort-Object -Unique
    if (-not (Test-Path $summaryPath)) {
        Add-Result 'HG-8' 'Generator summary lists every changed file' $false 'generator-summary.md missing'
    } else {
        $summary = Get-Content $summaryPath -Raw
        $missing = @($changed | Where-Object { $summary -notmatch [regex]::Escape($_) })
        Add-Result 'HG-8' 'Generator summary lists every changed file' ($missing.Count -eq 0) $(if ($missing.Count -eq 0) { "$($changed.Count) changed files, all listed" } else { 'Not listed: ' + ($missing -join ', ') })
    }
}

# Write checks.md
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' } | ForEach-Object { $_.Id })
$verdictLine = if ($failed.Count -eq 0) { 'HARD_GATES: PASS' } else { "HARD_GATES: FAIL ($($failed -join ', '))" }
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# Automated hard-gate results')
[void]$md.AppendLine('')
[void]$md.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz') by scripts/harness-check.ps1 on commit $(git rev-parse --short HEAD 2>$null)")
[void]$md.AppendLine('')
[void]$md.AppendLine('| Gate | Check | Result |')
[void]$md.AppendLine('|---|---|---|')
foreach ($r in $results) { [void]$md.AppendLine("| $($r.Id) | $($r.Name) | $($r.Result) |") }
[void]$md.AppendLine('')
[void]$md.AppendLine('## Evidence')
foreach ($r in $results) { [void]$md.AppendLine(''); [void]$md.AppendLine("### $($r.Id) $($r.Result)"); [void]$md.AppendLine('```'); [void]$md.AppendLine($r.Evidence); [void]$md.AppendLine('```') }
if (Test-Path (Join-Path $out 'coverage.md')) { [void]$md.AppendLine(''); [void]$md.AppendLine('## Coverage by layer'); [void]$md.AppendLine(''); [void]$md.AppendLine((Get-Content (Join-Path $out 'coverage.md') -Raw)) }
[void]$md.AppendLine('')
[void]$md.AppendLine($verdictLine)
Set-Content -Path (Join-Path $out 'checks.md') -Value $md.ToString() -Encoding UTF8

Write-Host ''
Write-Host $verdictLine -ForegroundColor $(if ($failed.Count -eq 0) { 'Green' } else { 'Red' })
Write-Host "Details: .harness\output\checks.md"
if ($failed.Count -eq 0) { exit 0 } else { exit 1 }
