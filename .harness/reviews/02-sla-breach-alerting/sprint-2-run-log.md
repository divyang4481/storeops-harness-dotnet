# Run log — 02-sla-breach-alerting sprint 2
RUN_ID: 02-sla-breach-alerting-s2-202610201730
SPRINT: 2          VERDICT: PASS   SCORE: 100
ITERATIONS: 1 of 3 ESCALATED: no   BASE_REF: 1171aae   HEAD: 62bf247
ENVIRONMENT: claude.ai/code cloud   MODEL: not reported for generator/evaluator (monitor: claude-opus-5-5)

Last sprint of the feature (2 of 2). Feature STATUS: DONE (archived progress.md).
Timeline (progress.md HISTORY): sprint 2 iter 1 started 17:22Z (BASE_REF 1171aae); GENERATOR_DONE 17:27Z (9/9
self-checked, deviations flagged); VERDICT PASS 100 17:29Z; archived; committed as 62bf247 ("sprint 2 PASS score 100
(iter 1)"); feature DONE. The working tree was clean when the Monitor started.
Source note: as the orchestrator instructed, this log uses the archived `.harness/reviews/02-sla-breach-alerting/progress.md`,
not `.harness/output/progress.md`.

## Iterations
| Iter | HG-1..HG-8 (P/F) | Failed check ids | Verdict | Score |
|---|---|---|---|---|
| 1 | P P P P P P P P | none (17/17 passed, B5 N/A) | PASS | 100 |

Gate evidence (sprint-2-checks.md): tests 133 passed / 0 failed (9 new escalation tests; 124 → 133); architecture
tests 15/15; coverage service 96.5%, routes 100%, shared 88.2%, overall 91% (sprint 1: 96.3 / 100 / 87.9 / 90.2);
HG-6 and HG-7 0 matches; HG-8 "12 changed files, all listed". D1–D4 all 100%. No endpoint added (B5 N/A). The endpoint
count stays at 10.

Notes:
- **Generator changed repository signatures beyond the contract (disclosed, E3 PASS).** The contract's file list said
  `IActivitySlaBreachRepository.cs` only gains `ListUnescalatedAsync`. The Generator also changed `AddAsync` to return
  `Task<bool>` (atomic add-if-absent) and `UpdateAsync` to `(current, updated, ct)` returning `Task<bool>` (atomic
  compare-and-swap), both backed by `ConcurrentDictionary.TryAdd` / `TryUpdate`. This addresses sprint-1 Evaluator
  advisory 1 (the check-then-add breach-marker race). The only caller is `ActivitySlaMonitor`. A sweep that loses the
  race skips publishing. Disclosed under Known gaps; the Evaluator accepted it under E3. Because sprint 1 was a PASS,
  the advisory was never formal carry-over, so this change was made outside any contract.
- Other disclosed deviations (E3 PASS): `SlaSweepResult.cs` (comment only) and `RecordingSubscriber.cs` (additive
  constructor with a shared ordered log, needed to assert the order in AC-2.6). The contract's AC-2.1–2.9 wording
  required both.
- **Spec → contract traceability gap (Evaluator advisory 1, Generator known gap 2).** `ActivitySlaMonitor.cs:112`
  computes escalation from the activity's *current* `DueAt`, re-read with `GetAsync`, not from the `DueAt` stored on
  the `ActivitySlaBreach` marker. So a postponed due date also postpones escalation, and a cleared `DueAt` skips
  escalation entirely. The APPROVED spec, assumption 6, says: "Moving the due date does not cancel a pending escalation
  unless the priority drops or the task is Done". Its out-of-scope list (line 26) also excludes re-arming the SLA on
  a `DueAt` move. The spec's business rule (line 65, "`now >= DueAt + EscalationGracePeriodMinutes`") and the
  contract's escalation rule both say "DueAt" without saying which one. None of AC-2.1–2.9 moves or clears `DueAt`
  after a breach. The Evaluator grades only against the contract, so it correctly passed the code and raised the
  conflict as an advisory. This is the first time in the archive that an approved spec assumption did not reach any AC.
- checks.md was generated on commit 68e60e8 with the sprint code uncommitted in the working tree. This is the same
  pattern as all three earlier sprints.
- HG-8 count matched this time. It reported 12 files, and `git diff --name-only 1171aae 62bf247` without `.harness/`
  also has 12 (8 src, 3 tests, README.md). The off-by-one seen in 01 s2 and 02 s1 did not recur. The artefacts still
  do not say which file HG-8 left out in those sprints.

## Estimated token cost
| Invocation | Agent | Chars read | Chars written | Est. tokens | Measured (orchestrator) |
|---|---|---|---|---|---|
| sprint 2 iter 1 | Generator | 42,063 | 48,417 | 26,620 | ~75,000 |
| sprint 2 iter 1 | Evaluator | 91,858 | 8,169 | 29,007 | ~61,000 |
| sprint 2 | Monitor | 66,275 | 13,700 | 23,994 | not reported |
TOTAL_EST_TOKENS: 79,621   (measured, excluding Monitor: ~136,000)

Method: tokens ≈ (chars read + chars written) / 4 + 4,000 fixed overhead per agent invocation (system prompt,
tool schemas, CLAUDE.md). Chars = byte size (`wc -c`) of every file the agent was instructed to read/write, plus
checks.md for the Evaluator. This is an estimate for trend comparison, not billing. The Planner ran once for the
feature, and its cost is in the sprint-1 log.

Inputs per row:
- Generator read: agent file 2,453 + skills 19,253 (app-context, architecture-principles, dotnet-coding-conventions,
  api-and-event-patterns, how-to-test) + contract 8,754 + spec.md 11,603. Written: 12 changed files 41,772
  (whole-file size at 62bf247 via `git cat-file -s`: src 13,925, tests 18,584, README 9,263) + generator-summary 6,645.
- Evaluator read: agent file 1,674 + skills 19,711 (app-context, architecture-principles, how-to-review,
  evaluation-criteria, how-to-test) + contract 8,754 + spec.md 11,603 + generator-summary 6,645 + checks.md 1,699
  + touched files 41,772 (proxy for `git diff 1171aae`). Written: evaluator-feedback.md 8,169.
- Monitor read: agent file 1,095 + app-context 3,338 + run-logging 2,281 + progress.md 1,037 + contract 8,754
  + generator-summary 6,645 + evaluator-feedback 8,169 + checks 1,699 + three earlier run-logs 20,802 + INDEX.md 852
  + spec.md 11,603 (read to confirm the traceability item). Written: this log ~13,500 + INDEX row ~200.

Estimate vs measured: the file-size method gives ~56k for generator + evaluator, while the orchestrator measured
~136k (about 2.4x lower; earlier sprints were 3.7x, 2.3x and 3.3x). Measured cost went down from sprint 1: generator
92k → 75k, evaluator 67k → 61k (9 ACs and 12 files, against 11 ACs and 18 files).

## Quality trend
- Run-logs compared: 01 s1, 01 s2, 02 s1, plus this one. That is 4 sprints, and no check id has failed in any of
  them. There are no recurring failed check ids and no skill drift signals by the run-logging rule.
- Iterations per sprint: 1, 1, 1, 1 (average 1.0). Escalations so far: 0. Scores: 100, 100, 100, 100.
- E3 deviations: 0 (01 s1), 0 (01 s2), 1 (02 s1, Program.cs), 3 (02 s2). Every one was disclosed and accepted. The
  upward trend is a signal that contract file lists are less complete for features that change existing internals.
- New category: a **spec → contract traceability gap** (above). The four sprints have no check that would catch it,
  because every gate and check grades against the contract. All-PASS scores do not prove spec conformance.
- Sprint-1 open items, status after this sprint:
  - Breach-marker race: fixed (atomic `TryAdd` / `TryUpdate`). The new CAS-loss branches (`ActivitySlaMonitor.cs:79-82`,
    `:119-122`) have no direct test (Evaluator advisory 2, Generator known gap 3).
  - `UpdateAsync` with no callers: resolved (the escalation pass uses it).
  - Untested tick loop in `ActivitySlaSweepService.ExecuteAsync`: still open. The contract kept the hosted service
    unchanged.
  - Hosted sweep running on the real clock in `StoreOpsApiFactory` tests: not mentioned in sprint-2 artefacts; still open.
- Skill files have the same byte sizes as in the sprint-1 log (Generator set 19,253, Evaluator set 19,711,
  sprint-decomposition 3,102, run-logging 2,281, app-context 3,338). None of the earlier proposals has been applied.

## Open items (last sprint, verdict PASS; for the developer — not formal carry-over)
1. **Decision needed: escalation vs a moved `DueAt`.** The current behaviour (postpone, or skip if cleared) differs
   from approved spec assumption 6 (moving the due date does not cancel a pending escalation). Either confirm the
   current behaviour with a spec change request, or fix it in a follow-up to use the marker's `DueAt`, with an AC that
   breaches A, PATCHes `dueAt` later, sweeps at the original `DueAt + grace`, and expects one SLA Escalation alert.
2. CAS-loss branches untested: add a repository-level concurrency test, or a monitor test with a stub repository
   returning `false`.
3. `ActivitySlaSweepService.ExecuteAsync` tick loop untested (from sprint 1).
4. Default-config hosted sweep running on the real clock in `StoreOpsApiFactory` tests (from sprint 1).
5. Spec assumption 3 asked the product owner whether the activity's assignee should also get the breach alert. No
   artefact records an answer.

## Skill file feedback (proposals, not applied)
No check failed, so none of these is a drift signal by the run-logging rule. The first one fixes a real gap.
- Spec→contract traceability gap → `.harness/skills/sprint-decomposition/SKILL.md` → add a rule and a contract
  section `## Spec traceability`: a table that maps every business rule and every behaviour-changing assumption in
  spec.md ("Open questions / assumptions", "Out of scope" items that state a rule) to the AC that proves it, or to
  "deferred to sprint N" / "not tested: reason". An assumption with no AC must not be marked silently covered. The rule
  should also say that when a value can come from two places (the entity's current field or a stored snapshot such as
  a marker), the rule names the source explicitly ("the `DueAt` recorded on the breach marker").
- Same gap, Evaluator side → `.harness/skills/how-to-review/SKILL.md` (and `.harness/skills/evaluation-criteria/SKILL.md`
  if a check is added) → keep grading against the contract, but add a non-critical check, e.g. "B6: implementation
  contradicts no APPROVED spec assumption; conflicts cited as spec line vs file:line". Then a conflict like this one
  shows up as a CONDITIONAL_PASS carry-over, not only as an advisory. Alternative with no new check: require the
  Evaluator to read the spec's assumptions section and list any conflict under a fixed "Spec conflicts" heading that
  the Monitor and orchestrator always surface.
- Advisory acted on outside a contract (repository signature change) → `CLAUDE.md` §3 step 4 routing and
  `.harness/skills/sprint-decomposition/SKILL.md` → when a PASS sprint has Evaluator advisories that the next sprint
  should address, the orchestrator or Planner adds them to the next contract under `## Carry-over (advisory)` with the
  affected signatures in "Files expected to change". Then the change is planned, not an E3 deviation.
  Also `.harness/agents/generator.agent.md` → a public interface signature change that is not in the contract must be
  listed under Known gaps together with every caller (this sprint did that; make it a rule).
- CAS-loss branches untested → `.harness/skills/how-to-test/SKILL.md` → add a rule: each `false` branch of an atomic
  claim (`TryAdd` / `TryUpdate` / compare-and-swap) that skips a publish gets one test with a repository stub that
  returns `false`, asserting no event is published.
- Carried from the sprint-1 log, still not applied: Program.cs/composition-root rule (sprint-decomposition), atomic
  once-only markers (api-and-event-patterns; now shown to work in code, so the rule should record it), fake-TimeProvider
  tick test (how-to-test), stale "9 endpoints" in `.harness/skills/app-context/SKILL.md:39`, HG-8 counting rule
  (how-to-review / harness-check), archived-progress input path (run-logging / monitor.agent.md), and a standard
  "Measured (orchestrator)" column (run-logging).
- Before the Cognizant workstation runs → apply at least the traceability and carry-over proposals first. Otherwise
  the next two features run with the same gap, and their results cannot be compared with these four sprints as a
  before/after skill change.

## Feature-level summary (cloud session, features 01 and 02)
| Feature | Sprints | ACs | Iterations per sprint | Verdicts (score) | Escalations | Est. tokens (file-size) | Measured tokens, excl. Monitor |
|---|---|---|---|---|---|---|---|
| 01-shift-handover-bulk-update | 2 | 14 | 1, 1 | PASS 100, PASS 100 | 0 | 150,795 (86,445 + 64,350) | ~390,000 (planner ~117k; generator ~88k + ~67k; evaluator ~66k + ~52k) |
| 02-sla-breach-alerting | 2 | 20 | 1, 1 | PASS 100, PASS 100 | 0 | 166,889 (87,268 + 79,621) | ~364,000 (planner ~69k; generator ~92k + ~75k; evaluator ~67k + ~61k) |
| **Total** | 4 | 34 | 4 iterations / 4 sprints | 4 PASS, avg 100 | 0 | 317,684 | ~754,000 |

- Environment: claude.ai/code cloud session (Linux, `scripts/*.sh`). The next two features run on the Cognizant
  workstation (Windows, `scripts/*.ps1`, corporate NuGet feed, no `git push`). Expect environment-specific first-run
  failures (restore/proxy) that these four sprints did not exercise.
- Measured per-agent averages: Planner ~93k per feature, Generator ~80k per sprint, Evaluator ~62k per sprint.
  Measured cost is about 2.3–3.7x the file-size estimate. That ratio is the baseline for the Windows runs.
- Not exercised yet: FAIL routing, iteration 2–3, INVALID verdict handling, CONDITIONAL_PASS carry-over, and
  escalation. All four sprints passed on the first iteration, so the feedback loop has not been tested end to end.
- The main quality finding is the spec→contract traceability gap in 02 s2. It went past every gate because gates
  and checks only see the contract.
