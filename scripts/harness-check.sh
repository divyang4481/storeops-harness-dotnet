#!/usr/bin/env bash
# Deterministic hard-gate checks for the Evaluator agent (bash version of scripts/harness-check.ps1).
# Runs ALL gates, writes .harness/output/checks.md ending with "HARD_GATES: PASS" or "HARD_GATES: FAIL (HG-x, ...)".
# Usage: ./scripts/harness-check.sh [BASE_REF]   (BASE_REF enables HG-8 traceability against generator-summary.md)
set -uo pipefail
cd "$(dirname "$0")/.."
BASE_REF="${1:-}"
OUT=.harness/output; COV=$OUT/coverage
mkdir -p "$OUT"; rm -rf "$COV"
IDS=(); NAMES=(); RES=(); EVID=()
add() { IDS+=("$1"); NAMES+=("$2"); RES+=("$3"); EVID+=("$4"); echo "$1 $2: $3"; }
tailn() { tail -n "${2:-15}" <<<"$1" | tr '|' '/'; }

dotnet restore StoreOps.sln >/dev/null

FMT=$(dotnet format StoreOps.sln --verify-no-changes --no-restore 2>&1); [[ $? -eq 0 ]] && add HG-1 "Format (dotnet format --verify-no-changes)" PASS "no changes needed" || add HG-1 "Format (dotnet format --verify-no-changes)" FAIL "$(tailn "$FMT")"

BUILD=$(dotnet build StoreOps.sln -c Release --no-restore 2>&1); BUILD_OK=$?
if [[ $BUILD_OK -eq 0 ]]; then add HG-2 "Build, 0 warnings (analyzers + StyleCop + nullable)" PASS "0 warnings, 0 errors"
else add HG-2 "Build, 0 warnings (analyzers + StyleCop + nullable)" FAIL "$(grep -E ' (error|warning) [A-Z]+[0-9]+' <<<"$BUILD" | sed 's/ \[.*//' | sort -u | tail -n 20 | tr '|' '/')"; fi

if [[ $BUILD_OK -eq 0 ]]; then
  TEST=$(dotnet test StoreOps.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory "$COV" 2>&1)
  [[ $? -eq 0 ]] && add HG-3 "All tests pass (xUnit + WebApplicationFactory)" PASS "$(grep -E 'Passed!|Total tests' <<<"$TEST" | tr '\n' ' ')" \
                 || add HG-3 "All tests pass (xUnit + WebApplicationFactory)" FAIL "$(grep -E 'Failed |Assert|Expected|Actual' <<<"$TEST" | tail -n 25 | tr '|' '/')"
  ARCH=$(dotnet test tests/StoreOps.Api.Tests/StoreOps.Api.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~StoreOps.Api.Tests.Architecture' 2>&1)
  [[ $? -eq 0 ]] && add HG-4 "Architecture tests (boundaries, cycles, event bus, staff read-only, AppError)" PASS "$(grep -E 'Passed!' <<<"$ARCH")" \
                 || add HG-4 "Architecture tests (boundaries, cycles, event bus, staff read-only, AppError)" FAIL "$(grep -E 'Failed|Cross-module|Raw exception|Circular|injects' <<<"$ARCH" | tail -n 15 | tr '|' '/')"
  dotnet tools/StoreOps.CoverageGate/bin/Release/net8.0/StoreOps.CoverageGate.dll "$COV" "$OUT/coverage.md" && add HG-5 "Coverage thresholds (service 80 / routes 70 / shared 60 / overall 70)" PASS "see coverage table below" \
                 || add HG-5 "Coverage thresholds (service 80 / routes 70 / shared 60 / overall 70)" FAIL "see coverage table below"
else
  add HG-3 "All tests pass" FAIL "skipped: build failed"; add HG-4 "Architecture tests" FAIL "skipped: build failed"; add HG-5 "Coverage thresholds" FAIL "skipped: build failed"
fi

RAW=$(grep -rnE 'throw[[:space:]]+new[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' src/StoreOps.Api/Modules src/StoreOps.Api/Common/Http --include=*.cs | grep -vE 'throw[[:space:]]+new[[:space:]]+[A-Za-z_][A-Za-z0-9_]*Error\b' || true)
[[ -z "$RAW" ]] && add HG-6 "No raw exception throws in services/routes" PASS "0 matches" || add HG-6 "No raw exception throws in services/routes" FAIL "$(tr '|' '/' <<<"$RAW")"

CROSS=""
for dir in src/StoreOps.Api/Modules/*/; do
  m=$(basename "$dir")
  hits=$(grep -rnoE 'StoreOps\.Api\.Modules\.[A-Za-z]+' "$dir" --include=*.cs | grep -v "StoreOps.Api.Modules.$m\$" || true)
  [[ -n "$hits" ]] && CROSS+="$hits"$'\n'
done
[[ -z "${CROSS//$'\n'/}" ]] && add HG-7 "No cross-module internal references (Contracts/ only)" PASS "0 matches" || add HG-7 "No cross-module internal references (Contracts/ only)" FAIL "$CROSS"

if [[ -n "$BASE_REF" ]]; then
  if [[ ! -f "$OUT/generator-summary.md" ]]; then add HG-8 "Generator summary lists every changed file" FAIL "generator-summary.md missing"
  else
    CHANGED=$( { git diff --name-only "$BASE_REF" -- src tests tools; git ls-files --others --exclude-standard -- src tests tools; } | sort -u | sed '/^$/d')
    MISSING=$(while read -r f; do [[ -n "$f" ]] && ! grep -qF "$f" "$OUT/generator-summary.md" && echo "$f"; done <<<"$CHANGED")
    [[ -z "$MISSING" ]] && add HG-8 "Generator summary lists every changed file" PASS "$(wc -l <<<"$CHANGED" | tr -d ' ') changed files, all listed" \
                        || add HG-8 "Generator summary lists every changed file" FAIL "Not listed: $(tr '\n' ' ' <<<"$MISSING")"
  fi
fi

FAILED=(); for i in "${!IDS[@]}"; do [[ "${RES[$i]}" == FAIL ]] && FAILED+=("${IDS[$i]}"); done
if [[ ${#FAILED[@]} -eq 0 ]]; then VERDICT="HARD_GATES: PASS"; else VERDICT="HARD_GATES: FAIL ($(IFS=,; echo "${FAILED[*]}" | sed 's/,/, /g'))"; fi
{
  echo "# Automated hard-gate results"; echo
  echo "Generated: $(date '+%Y-%m-%d %H:%M:%S %z') by scripts/harness-check.sh on commit $(git rev-parse --short HEAD 2>/dev/null)"; echo
  echo "| Gate | Check | Result |"; echo "|---|---|---|"
  for i in "${!IDS[@]}"; do echo "| ${IDS[$i]} | ${NAMES[$i]} | ${RES[$i]} |"; done
  echo; echo "## Evidence"
  for i in "${!IDS[@]}"; do echo; echo "### ${IDS[$i]} ${RES[$i]}"; echo '```'; echo "${EVID[$i]}"; echo '```'; done
  [[ -f "$OUT/coverage.md" ]] && { echo; echo "## Coverage by layer"; echo; cat "$OUT/coverage.md"; }
  echo; echo "$VERDICT"
} > "$OUT/checks.md"
echo; echo "$VERDICT"; echo "Details: .harness/output/checks.md"
[[ ${#FAILED[@]} -eq 0 ]]
