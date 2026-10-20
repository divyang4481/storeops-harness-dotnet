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
  for _ in $(seq 1 40); do curl -sf "$B/alerts" >/dev/null && break; sleep 0.5; done
  ID=$(curl -sf -X POST "$B/activities" -H 'content-type: application/json' \
        -d '{"storeId":"ST-001","title":"Smoke stock count","assigneeStaffId":"S-1001"}' | sed -E 's/.*"id":"([^"]+)".*/\1/')
  curl -sf -X PATCH "$B/activities/$ID" -H 'content-type: application/json' -d '{"status":"Cancelled"}' >/dev/null
  curl -sf "$B/alerts?storeId=ST-001" | grep -q ACTIVITY_CANCELLED || { echo "FAILED: no alert via event bus"; exit 1; }
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "$B/activities/00000000-0000-0000-0000-000000000001")
  [[ "$CODE" == "404" ]] || { echo "FAILED: expected 404, got $CODE"; exit 1; }
  echo "Smoke test passed"
fi

echo; echo "ALL CHECKS PASSED"
