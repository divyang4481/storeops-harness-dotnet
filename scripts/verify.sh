#!/usr/bin/env bash
# One-command quality gate for StoreOps (Linux/macOS/Git Bash):
# SDK check -> restore -> lint -> build -> test -> live smoke test.
# Usage: ./scripts/verify.sh            (all steps)
#        SKIP_SMOKE=1 ./scripts/verify.sh
set -euo pipefail
cd "$(dirname "$0")/.."
PORT="${PORT:-5080}"

step() { echo; echo "==> $1"; }

step "SDK check (needs .NET 8 SDK or newer)"; dotnet --list-sdks; dotnet --version
step "Restore";                               dotnet restore StoreOps.sln
step "Lint: dotnet format --verify-no-changes"; dotnet format StoreOps.sln --verify-no-changes --no-restore
step "Build: Release, warnings as errors";    dotnet build StoreOps.sln -c Release --no-restore
step "Test: xUnit + WebApplicationFactory";   dotnet test StoreOps.sln -c Release --no-build --logger "console;verbosity=normal"

if [[ "${SKIP_SMOKE:-0}" != "1" ]]; then
  step "Smoke test: run API on http://127.0.0.1:$PORT"
  dotnet src/StoreOps.Api/bin/Release/net8.0/StoreOps.Api.dll --urls "http://127.0.0.1:$PORT" >/dev/null 2>&1 &
  PID=$!
  trap 'kill $PID 2>/dev/null || true' EXIT
  B="http://127.0.0.1:$PORT/api"
  for _ in $(seq 1 40); do curl -sf -H 'X-Staff-Id: S-1001' "$B/alerts" >/dev/null && break; sleep 0.5; done
  # ST-001 department lead creates and blocks a task; the store manager must receive an ESCALATION alert via the event bus.
  ID=$(curl -sf -X POST "$B/activities" -H 'X-Staff-Id: S-1002' -H 'content-type: application/json' \
        -d '{"title":"Smoke stock count","category":"Restocking","assigneeStaffId":"S-1003"}' | sed -E 's/.*"id":"([^"]+)".*/\1/')
  curl -sf -X PATCH "$B/activities/$ID" -H 'X-Staff-Id: S-1002' -H 'content-type: application/json' -d '{"status":"Blocked"}' >/dev/null
  curl -sf -H 'X-Staff-Id: S-1001' "$B/alerts?type=Escalation" | grep -q "$ID" || { echo "FAILED: no escalation alert via event bus"; exit 1; }
  CODE=$(curl -s -o /dev/null -w '%{http_code}' -H 'X-Staff-Id: S-1001' "$B/activities/00000000-0000-0000-0000-000000000001")
  [[ "$CODE" == "404" ]] || { echo "FAILED: expected 404, got $CODE"; exit 1; }
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "$B/activities")
  [[ "$CODE" == "401" ]] || { echo "FAILED: expected 401 without X-Staff-Id, got $CODE"; exit 1; }
  echo "Smoke test passed"
fi

echo; echo "ALL CHECKS PASSED"
