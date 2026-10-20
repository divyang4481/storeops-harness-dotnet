# Skill: evaluation-criteria (Evaluator; Planner reads the dimension list to write checkable ACs)

**Purpose:** the grading model that converts variable Generator output into a deterministic verdict.
The Evaluator decides only **binary, evidence-backed check results**. Weights, thresholds and verdict rules are
implemented in code (`tools/StoreOps.HarnessVerdict/VerdictCalculator.cs`, covered by `VerdictCalculatorTests`)
so the same check results always produce the same verdict. If this file and the code disagree, the code wins and
the Monitor flags the drift.

## Dimensions (weights sum to 100)
| Dim | Name | Weight | Hard gates (automated, deterministic) | Failure mode it guards |
|---|---|---|---|---|
| D1 | Architecture conformance | 35 | HG-4 architecture tests, HG-6 raw-throw scan, HG-7 cross-module scan | FM1, FM2, FM4 |
| D2 | Functional correctness vs ACs | 35 | HG-3 all tests pass, HG-5 coverage per layer | FM3 |
| D3 | Code quality & conventions | 20 | HG-1 dotnet format, HG-2 Release build 0 warnings (StyleCop, analyzers, nullable) | drift from standards |
| D4 | Traceability & handoff | 10 | HG-8 generator-summary lists every changed file | unreviewable changes |

Why these weights: the client's four failure modes are architectural (D1) and test-quality (D2) failures, so they
carry 70%. D3 is mostly enforced by tooling already (cheap to fix, lower weight). D4 keeps the audit trail honest.

## Checklist (IDs are stable — the Monitor tracks them across runs)
| ID | Dim | Check (PASS criterion) | Critical |
|---|---|---|---|
| A1 | D1 | Every cross-module side effect is an event published via `IEventBus.PublishAsync` after persistence and handled by an `IEventSubscriber<T>` in the receiving module (R2) | yes |
| A2 | D1 | No changed file injects, references or calls another module's service/repository/entity; only `Contracts/*` and `Common/*` cross boundaries (R1) | yes |
| A3 | D1 | Routes only bind/call/map; rules live in services; repositories are storage-only (R4) | no |
| A4 | D1 | Correct `AppError` subtype per situation (400 validation, 401, 403 authorisation, 404 parent-first, 409 state) (R3) | no |
| A5 | D1 | Reports module writes nothing outside Reports; if Reports untouched, PASS citing the diff (R5) | yes |
| A6 | D1 | New events are `sealed record` in `Contracts/<Publisher>/`, implement `IIntegrationEvent`, subscribers registered in the subscriber's module | no |
| B1 | D2 | Every AC in the sprint contract is implemented as specified (route, verb, payload, outcome) | yes |
| B2 | D2 | Every AC has a test tagged `// AC-N.M` that asserts the THEN business outcome, not only the status code (FM3) | yes |
| B3 | D2 | Negative paths named in the contract are tested (400/401/403/404/409, partial failure) | no |
| B4 | D2 | No existing test deleted, skipped or weakened; base invariants (R8) still hold | yes |
| B5 | D2 | If endpoints were added: `EndpointInventoryTests.ExpectedEndpoints` and README updated; else N/A | no |
| C1 | D3 | Conventions from `dotnet-coding-conventions` followed in changed files (one type per file, naming, record doc style, DI in `<Module>Module.cs`) | no |
| C2 | D3 | No new analyzer suppressions (`NoWarn`, `#pragma warning disable`, `.editorconfig` severity, `SuppressMessage`) | yes |
| C3 | D3 | Time via `TimeProvider`; async with `CancellationToken` passed through; no blocking calls | no |
| C4 | D3 | Inputs validated, including undefined enum values and empty/oversized collections | no |
| E1 | D4 | `generator-summary.md` has an AC self-check table naming the proving test for every AC | no |
| E2 | D4 | Known gaps are stated honestly; no AC marked done without a passing test | yes |
| E3 | D4 | Changed files stay within the contract's "Files expected to change", or each deviation is explained | no |

Outcomes: `PASS`, `FAIL`, `UNVERIFIABLE` (no evidence found → counts as FAIL), `N/A` (trigger absent; never for
critical checks). Evidence column is mandatory for every row.

## Verdict rules (applied by the tool, in this order)
0. **INVALID** — checks.md lacks the `HARD_GATES` line, a row has an unknown outcome/dimension, a critical check is
   N/A, an id is duplicated, or a dimension has no scored check. Orchestrator re-runs the Evaluator once, then escalates.
1. **FAIL** — any automated hard gate failed (HG-1…HG-8).
2. **FAIL** — any critical check is FAIL or UNVERIFIABLE.
3. Score = Σ weight(D) × passed(D)/scored(D). **PASS** ≥ 90 · **CONDITIONAL_PASS** 75–89.9 · **FAIL** < 75.

CONDITIONAL_PASS advances the sprint; each failed non-critical check becomes a "Carry-over" item the Planner's next
contract must include (or, on the last sprint, an open item in the run-log). FAIL returns "Required fixes" to the
Generator. Example: A1–A6 pass, B2 passes but B3 fails (one negative path untested), all else passes, gates pass →
D2 = 4/5 = 80% → score = 35 + 28 + 20 + 10 = 93 → **PASS** with B3 noted. If B2 had failed instead → critical → **FAIL**.

## Iteration budget and escalation
Max **3 Generator iterations per sprint**. Escalate (orchestrator writes `.harness/output/escalation.md`) when:
iteration 3 is not PASS/CONDITIONAL_PASS · the verdict is INVALID twice · the Generator reports a contract
conflicting with R1–R8 · the same critical check fails in two consecutive iterations with the same evidence
(no progress).
