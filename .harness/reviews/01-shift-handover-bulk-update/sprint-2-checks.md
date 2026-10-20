# Automated hard-gate results

Generated: 2026-10-20 17:05:33 +0000 by scripts/harness-check.sh on commit a4766ce

| Gate | Check | Result |
|---|---|---|
| HG-1 | Format (dotnet format --verify-no-changes) | PASS |
| HG-2 | Build, 0 warnings (analyzers + StyleCop + nullable) | PASS |
| HG-3 | All tests pass (xUnit + WebApplicationFactory) | PASS |
| HG-4 | Architecture tests (boundaries, cycles, event bus, staff read-only, AppError) | PASS |
| HG-5 | Coverage thresholds (service 80 / routes 70 / shared 60 / overall 70) | PASS |
| HG-6 | No raw exception throws in services/routes | PASS |
| HG-7 | No cross-module internal references (Contracts/ only) | PASS |
| HG-8 | Generator summary lists every changed file | PASS |

## Evidence

### HG-1 PASS
```
no changes needed
```

### HG-2 PASS
```
0 warnings, 0 errors
```

### HG-3 PASS
```
Passed!  - Failed:     0, Passed:   113, Skipped:     0, Total:   113, Duration: 2 s - StoreOps.Api.Tests.dll (net8.0) 
```

### HG-4 PASS
```
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 106 ms - StoreOps.Api.Tests.dll (net8.0)
```

### HG-5 PASS
```
see coverage table below
```

### HG-6 PASS
```
0 matches
```

### HG-7 PASS
```
0 matches
```

### HG-8 PASS
```
6 changed files, all listed
```

## Coverage by layer

| Layer | Threshold | Covered / Total | Line coverage | Result |
|---|---|---|---|---|
| Service layer (Modules/*/*Service.cs, Subscribers) | 80% | 444 / 458 | 96.9% | PASS |
| Route layer (Modules/*/*Routes.cs) | 70% | 38 / 38 | 100% | PASS |
| Shared (Common/**, Contracts/**) | 60% | 143 / 164 | 87.2% | PASS |
| Overall (src/StoreOps.Api) | 70% | 973 / 1076 | 90.4% | PASS |

HARD_GATES: PASS
