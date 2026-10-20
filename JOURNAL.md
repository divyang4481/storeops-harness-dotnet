# Architecture Journal

Decisions, trade-offs and insights captured while building the StoreOps harness. Entries are in chronological
order. Each entry links to its evidence: a commit, a review artefact, or a run-log.

---

### 1. Baseline generation: what Claude Code got right, and where it needed guardrails
Generated the scaffold from the bootstrap prompt: .NET 8, five modules, Routes → Service → Repository, and the
`AppError` hierarchy. My workspace could not reach NuGet at first. I checked the API against the built-in .NET
analyzers, and type-checked the tests against hand-written stand-ins for xUnit and WebApplicationFactory.

**Insight:** "it compiles" against stand-ins is not "it passes". When the real StyleCop ran later it found SA1204
(member ordering), which the stand-ins could never catch. *This is why HG-2 must run the real toolchain, and why the
harness treats only automated tool output as a hard gate.*

### 2. An independent review found a real defect the tests missed
The first run on the Cognizant machine had a separate Claude session exercise the API. It found that
`PATCH {"status":7}` was **accepted and saved**. Undefined numeric enum values pass model binding in .NET.

- **Fixes:** `JsonStringEnumConverter(allowIntegerValues: false)`, plus an `Enum.IsDefined` check in the services.
  Both now have regression tests.
- **Insight:** this is FM3 in miniature. The tests checked the happy path and the status code, not "nothing invalid
  is stored". It became rule R8 in `architecture-principles`, and a pattern in `dotnet-coding-conventions`.

### 3. Aligning the baseline with the specification before building the harness
The first scaffold used my own status names (Planned/Completed). Section 3.3 of the brief defines TODO,
IN_PROGRESS, DONE and BLOCKED, along with priority, category, staff roles and alert types, and the four demo
features depend on these. I re-aligned the code before writing a single skill file.

- *Trade-off:* a large refactor (commit `84dda05`) instead of carrying a translation layer.
- I added stub authentication through an `X-Staff-Id` header, because "alerts for the authenticated user" and
  "delete: owner or store manager only" cannot be tested without a caller.
- *Assumption:* a real identity provider (Entra ID or Cognito) would map its claims to the same staff ID.

### 4. Decision: the verdict is computed in code, not by the model
- *Options:* (a) the Evaluator writes PASS or FAIL itself; (b) automated gates only; (c) the LLM makes binary
  per-check decisions with evidence, and code aggregates them. I chose (c): `tools/StoreOps.HarnessVerdict`, with 12
  determinism tests.
- *Why:* option (a) is lenient and drifts with phrasing. Option (b) cannot judge whether a test proves an
  acceptance criterion.
- *Insight:* forcing an evidence column (`file:line`) into every row did more for consistency than any amount of
  prompt wording. UNVERIFIABLE counting as FAIL closes the loophole of "no evidence, but looks fine".

### 5. Decision: architecture rules as xUnit tests, not an external dependency analyser
- *Options:* NetArchTest, depcruiser, or tests written in the solution. I wrote tests in the solution: a reflection
  test for constructor injection, plus source scans that give `file:line` evidence.
- *Why:* NuGet was blocked in one environment and Cognizant machines restrict installs. Anything that runs under
  `dotnet test` runs everywhere CI runs.
- *Proof it works:* a raw `throw` and a cross-module `using`, planted on purpose, were both caught with their file
  and line.

### 6. Decision: a fresh subagent per role, with file handoffs and a resumable state file
- The Evaluator must not see the Generator's reasoning.
- `progress.md` lets a run survive a closed session. That mattered because work was split between a cloud session
  and the Cognizant workstation.
- Folder-write protection on the user's machine (`.github/` and `.claude/` were rejected by the remote tools) is why
  all harness delivery goes through Git.

### 7. Two environments, one harness
I cannot push from the Cognizant workstation. The resulting workflow:
- harness and features 01–02 built in the claude.ai/code cloud session and pushed;
- on the workstation: `git pull`, then features 03–04;
- the submission zip is built there.

**Insight:** the harness never assumes a network path back. Its audit trail is plain files that travel with the
zip. The same PowerShell gate scripts passed on both machines, although only the bash versions had been exercised
before the first Cognizant run.

### 8. First-iteration passes: good news that hides an untested path
Features 01 and 02 passed all four sprints at score 100 on iteration 1. The Monitor pointed out that the FAIL →
fix → re-evaluate loop, carry-over and escalation were therefore **unexercised**.

- The evidence that they work comes from the verdict-tool tests and the routing table, not from a live run.
- *Open question for the reviewer:* should a harness demonstration deliberately include a failing iteration?
- *My answer:* yes, as a scheduled "fault injection" sprint (see REFLECTION.md).

### 9. The Monitor caught a governance failure: a spec assumption with no acceptance criterion
In run 02, escalation timing followed the task's *current* due date. The approved spec said a due date moved after
a breach must not postpone escalation. Every check passed, because no AC encoded that assumption. The Evaluator
could only raise it as an advisory.

- **Fix (skill refinement 1):** every contract now carries a "Spec traceability" table, and a new check, B6, fails
  any contradiction of an approved assumption.
- The defect itself was fixed **through the harness**, as change request 1: `@planner change …`, then approval, then
  sprint 3.
- *Insight:* the weakest link was not code generation. It was translating intent into the contract.

### 10. Cost is dominated by code and tool output, not skills
Measured subagent usage was roughly 50k to 120k tokens per invocation. The file-size estimate in the Monitor skill
undercounts by about 3.5 times, because tool output and source files read repeatedly are not counted.

- *Implication:* trimming skill files saves little. Scoping which source files each agent reads saves the most,
  which is why each contract lists "Files expected to change".
- *Next step:* have each agent report the files it actually read, so the estimate can be calibrated.

<!-- FINALIZE: Cognizant Claude appends entries 11+ for features 03–04 here (environment differences, iterations,
anything that escalated). Keep entries specific and evidence-linked. -->
