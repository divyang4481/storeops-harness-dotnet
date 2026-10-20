# Harness Design Brief — StoreOps (.NET 8)

**Author:** <your full name> (GitHub: divyang4481) · **Programme:** AI-Native Tech Architect, Capstone 1 (Build Track) ·
**Repository:** `storeops-harness-dotnet` · **Harness entry point:** `CLAUDE.md`

The harness exists to stop the four failure modes the client saw in its earlier AI-assisted experiment from reaching
`main`. **FM1:** importing another module's repository. **FM2:** throwing raw exceptions. **FM3:** tests that check
only status codes. **FM4:** writing state changes directly into sibling modules. Every element below is traced to
one of these.

```
@planner <feature> ──► Planner ──► spec.md + sprint-N-contract.md ──► STATUS: AWAITING APPROVAL ──► developer: APPROVED
                                                                                                        │
           ┌──────────────── FAIL (iteration < 3): required fixes with file:line ◄───────────┐         ▼
           ▼                                                                                │   for each sprint
      Generator ──► code + tests + generator-summary.md ──► Evaluator ──► harness-check (HG-1..8)
                                                              │          + check table (binary, evidence)
                                                              │          + VerdictCalculator (code) ──► VERDICT
     PASS / CONDITIONAL_PASS ──► archive to .harness/reviews/<slug>/ ──► local commit ──► Monitor ──► run-log + INDEX
     FAIL at iteration 3 · INVALID twice · GENERATOR_BLOCKED · no progress ──► escalation.md ──► developer / tech lead
```

---

## A. Intent decomposition

**Sprint boundaries follow architectural seams, not file types.** Each feature is cut so the first sprint delivers
the owning module's behaviour, publishing an event where needed. The second sprint delivers the reaction in the
subscribing module, wired only through that event. Feature 01 (shift handover) split into:
- Sprint 1: `PATCH /api/activities/bulk-status`, with partial failure handling, audit entries and one
  `ActivityStatusChangedEvent` per updated task.
- Sprint 2: `ShiftHandoverCompletedEvent`, raised once per handover, and the Alerts subscriber that notifies the
  incoming store manager.

Feature 02 (SLA breach) split into:
- Sprint 1: detect the breach and alert the department lead.
- Sprint 2: escalate to the store manager after a configurable grace period.

The reason for this cut is that each sprint then leaves `main` releasable and passing every hard gate. It also
means a module-boundary violation (FM1 or FM4) can only enter at a seam that one contract owns, so the Evaluator
looks for it in exactly one place. A sprint touches at most two modules and about 12 files, so the Generator's
context stays bounded (see Section D).

**Acceptance criteria are written to be testable, not subjective.** The `sprint-decomposition` skill sets five rules:
1. GIVEN names seeded data: the caller's `X-Staff-Id`, the store, and the existing task state.
2. WHEN is one HTTP call or one event.
3. THEN gives the status code *and* an observable business outcome: a stored value, an alert's recipient and type,
   or a count.
4. Every AC names the test that proves it (`Verify:`).
5. Every sprint has at least one negative AC.

Rule 3 is the direct counter to FM3: an AC with nothing to assert except a status code fails Evaluator check B2,
which is critical. A real example from `.harness/reviews/01-shift-handover-bulk-update/sprint-1-contract.md`:

```
AC-1.4  (negative) GIVEN caller S-1003, ST-001 activity B already Blocked and activity C (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{B,Blocked},{C,Blocked}]}
        THEN 200 with B: Rejected UNCHANGED (no audit entry, no event) and C: Updated Todo→Blocked; S-1001's
             GET /api/alerts?type=Escalation contains exactly one new alert referencing C and none for this request
             referencing B (proves exactly one ActivityStatusChangedEvent published, R2)
        Verify: BulkStatusEndpointTests.UnchangedStatus_IsRejectedWithoutEventOrAudit
```

The THEN clause proves the event-bus rule (R2) through something visible from outside: an alert raised by a
different module. The test can only pass if the event really was published once.

**Traceability from spec to contract was added after a real miss.** In run 02 every check passed, yet the
escalation timing contradicted an assumption in the approved spec: moving a due date must not postpone escalation.
The cause was that no AC encoded that assumption. The fix was skill refinement 1
(`.harness/reviews/SKILL-CHANGELOG.md`). Every contract now carries a "Spec traceability" table that maps each spec
rule or assumption to an AC, or defers it with a reason. A new check, B6, fails any sprint that contradicts an
approved assumption. The defect was then fixed through the harness itself, as change request 1 (sprint 3 of
feature 02).

## B. Governance framework

**Skill file strategy.** There are nine skills under `.harness/skills/`. Two are shared by every agent and kept
short (about 2,100 tokens together), so the context they cost is paid once per agent. The rest are role-specific:

| Skill | Read by | StoreOps-specific content (not generic) |
|---|---|---|
| app-context | all | 5 modules, seeded staff table (S-1001 …), the 10 endpoints, SLA sweep config, commands |
| architecture-principles | Planner, Generator, Evaluator | rules R1–R8, each mapped to FM1–FM4, its enforcing gate, and right/wrong code from this repo |
| sprint-decomposition | Planner | seam-based sizing, GIVEN/WHEN/THEN rules, traceability table, change-request flow |
| dotnet-coding-conventions | Generator | the exact StyleCop/CA/IDE rules that broke this build (SA1611 on records, IDE0005 implicit usings, CA1826 …) |
| api-and-event-patterns | Generator | Routes → Service → Repository recipe, `X-Staff-Id` caller pattern, event + subscriber wiring, atomic once-only markers |
| how-to-test | Generator, Evaluator | `CreateClientAs(TestStaff…)`, outcome assertions, `// AC-N.M` tags, coverage thresholds 80/70/60/70 |
| how-to-review | Evaluator | step-by-step review, evidence rules, HG-8 scope, feedback format |
| evaluation-criteria | Evaluator (Planner reads tables) | dimensions, weights, check IDs A1–E3 and B6, verdict rules |
| run-logging | Monitor | run-log format, token estimation method, drift signals |

The convention rules come from real failures, not a style guide. Every analyzer rule listed in
`dotnet-coding-conventions` broke a build at least once while this codebase was being created.

**The audit trail.** For each feature, `.harness/reviews/<slug>/` holds:
- the approved `spec.md`;
- per sprint, `sprint-N-contract.md`, `-generator-summary.md`, `-evaluator-feedback.md`, `-checks.md`, a progress
  snapshot and `-run-log.md`;
- the feedback from every failed iteration, kept rather than overwritten.

On top of that, `.harness/reviews/INDEX.md` has one row per sprint (run ID, verdict, score, iterations, escalation,
tokens), and `SKILL-CHANGELOG.md` ties every change to a skill back to the run-log that triggered it. The archive is
committed alongside the code, so anyone with repository access can read it, and it travels with the pull request:
the tech lead sees the decision trail next to the diff.

**How a recurring quality issue surfaces.** Check IDs are stable. The Monitor flags any ID that fails in two or
more sprints as a *skill drift signal* and names the skill that should have prevented it (A* → architecture skills,
B* → how-to-test, and so on). In this build the mechanism caught process drift rather than code drift. The Monitor
noted that its earlier proposals had not been applied because the skill files were byte-identical across runs. That
led to refinement 1, a separate human-reviewed commit, with 12 changes traced in the changelog.

**One rule, and what breaks without it.** R2 says event bus only: *"When something in module A must cause a change
in module B, A publishes an event record from `Contracts/A/` via `IEventBus.PublishAsync(...)` AFTER persisting its
own state; B subscribes with `IEventSubscriber<TEvent>`. A never injects B's service."* Take this rule away and the
shift-handover feature would naturally be written as `ActivityService` calling `AlertService.RaiseAsync`. That is
FM4. Activities would then depend on Alerts, so a second consumer such as Reports' metrics would need another direct
call. Any alert failure would also fail the handover request, because the event bus isolates subscriber failures
and a direct call does not. The rule is enforced three ways:
- **HG-4:** `ModuleTypes_DoNotInjectTypesFromOtherModules` fails on any constructor that injects a foreign module's
  type.
- **HG-7:** a source scan reports `file:line` for any cross-module reference.
- **Check A1** (critical) confirms that each new side effect really is published *after* persistence.

## C. Non-determinism strategy

**Dimensions and weights (sum = 100).**

| Dimension | Weight | Why it carries this weight for StoreOps |
|---|---|---|
| D1 Architecture conformance | 35 | FM1, FM2 and FM4 are architectural; they are what the standards team refused to accept |
| D2 Functional correctness vs ACs | 35 | FM3: tests that pass without proving behaviour are the most expensive failure to catch later |
| D3 Code quality and conventions | 20 | mostly enforced by tooling already (HG-1, HG-2), so cheap to fix; lower weight |
| D4 Traceability and handoff | 10 | keeps the audit trail honest; a wrong summary makes the review untrustworthy |

**Hard gates are deterministic tools, and each blocks a specific failure.**

| Gate | Tool | Failure mode it prevents | Why it cannot be a soft check |
|---|---|---|---|
| HG-1 | `dotnet format --verify-no-changes` | drift from conventions | the same rule CI enforces; a soft score would let CI fail after the harness says PASS |
| HG-2 | Release build, warnings as errors (StyleCop, CA 8.0-recommended, nullable) | unsafe or noncompliant code | an analyzer error is binary; scoring it would trade correctness for points |
| HG-3 | `dotnet test` | regressions | one failing test means behaviour is broken, whatever else scores well |
| HG-4 | architecture tests (reflection + source scan) | FM1, FM2, FM4 | boundary violations compound; one accepted import becomes a precedent |
| HG-5 | coverage gate: service 80 / routes 70 / shared 60 / overall 70 | FM3 (untested branches) | thresholds set by the client standards team, not negotiable per sprint |
| HG-6 / HG-7 | raw-throw and cross-module scans with `file:line` | FM2, FM1 | gives the Generator a precise location to fix without human help |
| HG-8 | changed files vs `generator-summary.md` | unreviewable changes | an undeclared change escapes review entirely |

Each dimension has at least one automated gate: D1 has HG-4/6/7, D2 has HG-3/5, D3 has HG-1/2, and D4 has HG-8.

**Turning variable output into a fixed verdict.** The LLM makes one decision per check: PASS, FAIL, UNVERIFIABLE
or N/A, with evidence (`file:line`, a test name or a gate ID) required in every row. Missing evidence becomes
UNVERIFIABLE, which counts as FAIL, so the system fails closed. The LLM never computes the verdict. The Evaluator
runs `tools/StoreOps.HarnessVerdict`, whose `VerdictCalculator` applies fixed rules in a fixed order:
1. Malformed input gives INVALID.
2. Any hard-gate failure gives FAIL.
3. Any critical check (A1, A2, A5, B1, B2, B4, C2, E2) that is not PASS gives FAIL.
4. Otherwise the weighted score decides: PASS at 90 or above, CONDITIONAL_PASS at 75 to 89.9, FAIL below 75.

Twelve unit tests, including `Decide_SameInput_AlwaysSameVerdict`, show that the same check results always produce
the same verdict.

*Worked example.* Suppose two Generator runs implement the same contract differently. Both pass all hard gates,
but one has no test for a 403 path the contract names. In that run B3 (non-critical) fails and every other check
passes, so D2 = 4/5 = 80% and the score = 35 + 28 + 20 + 10 = 93, giving **PASS** with B3 recorded. If the missing
test were the AC proof itself, B2 (critical) fails, giving **FAIL** whatever the score, and the Generator receives
`[B2][R7] tests/...:L — add test asserting <outcome>`. The orchestrator reads only the `VERDICT:` line of the tool's
output block, so routing is mechanical as well.

**Escalation path.** The orchestrator writes `escalation.md` and copies it into the archive when any of these
happens:
- iteration 3 ends without PASS or CONDITIONAL_PASS;
- the verdict is INVALID twice;
- the Generator replies `GENERATOR_BLOCKED` (the contract cannot be met within R1–R8);
- the same critical check fails twice with the same evidence (no progress).

The file contains the trigger, iterations used, the base and head commits, the blocking checks with evidence, the
Generator's known gaps, and three options: amend the contract, fix manually then `continue harness`, or revert. It
goes to the developer who started the run. Because it is archived, the tech lead and standards team see it in the
PR, and it is the record they act on.

## D. Architectural decisions

**1. The verdict is computed in code, not by the LLM.**
- *Alternatives:* the Evaluator states PASS or FAIL directly; or only automated gates decide.
- *Rationale:* an LLM judging "is this good enough" drifts with phrasing and leniency. Tools alone cannot judge
  whether a test proves an AC (B2) or whether a side effect crosses a boundary through the event bus (A1). Splitting
  the work keeps LLM judgement where it is needed, at the binary per-check level with mandatory evidence, and makes
  the aggregation reproducible and unit-tested.
- *Assumption:* binary checks with required evidence are stable across Evaluator runs. If two Evaluators disagreed
  on the same diff, the fix would be to split the check further, not to change the calculator.

**2. A fresh subagent per invocation, with file-only handoffs.**
- *Alternatives:* one long session running all roles; or roles sharing a conversation.
- *Rationale:* the Evaluator must not see the Generator's reasoning, or it grades intent instead of output. A fresh
  context per call also keeps each invocation's token budget predictable: skills of about 4.2k to 4.9k tokens, plus
  a scoped set of files. State lives in `progress.md`, so a run survives a closed session (`continue harness`). That
  mattered here, because runs were split across a cloud session and the Cognizant workstation.
- *Assumption:* the handoff files hold everything the next agent needs. The run-logs measured roughly 50k to 120k
  tokens per agent call, about 3.5 times the file-size estimate, so most of the cost is source code and tool output,
  not skills.

**3. Architecture rules enforced by xUnit tests in the solution, not an external dependency analyser.**
- *Alternatives:* NetArchTest or a depcruiser-style CLI.
- *Rationale:* the rules run wherever `dotnet test` runs, including the locked-down Cognizant machine and CI, with no
  extra tool to install or approve. The source scans also give `file:line` evidence, which the Generator needs to fix
  a violation unaided.
- *Assumption:* namespaces mirror folders (`StoreOps.Api.Modules.<Module>`). `dotnet format` and code review keep
  that true; a file in the wrong namespace could evade the scan.

**CI/CD relationship.** The harness comes *before* CI and does not replace it. `harness-check` runs the CI commands
plus harness-only gates (HG-5 to HG-8). CI on the pull request (`.github/workflows/ci.yml`) stays the independent,
non-AI gate. Harness files live in `.harness/` and `.claude/`, separate from `.github/`.
