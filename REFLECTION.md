# Reflection — demonstration runs

## What the harness did well
- **The architecture rules held without anyone stepping in.** The cloud session ran five sprints: feature 01
  (2 sprints), feature 02 (2 sprints) and change request 1. All passed every hard gate. HG-4 and HG-7 found zero
  boundary violations, and every cross-module effect was carried by an event: the handover, breach and escalation
  events, each with its own Alerts subscriber. The Generator never imported another module or called its service.
  For FM1 and FM4 to be absent across 5 sprints, the rules have to be followed in practice, not just written down.
- **Tests proved behaviour, not status codes (FM3).** Every acceptance criterion maps to a tagged test that checks a
  real outcome, such as an alert's recipient and message, a stored value, an audit entry, or an unchanged record for
  a rejected item. The Evaluator verified this criterion by criterion in each `sprint-N-evaluator-feedback.md`.
- **Deviations were disclosed, not hidden.** One example was the out-of-contract `Program.cs` change in
  `02/sprint-1`. HG-8 and check E3 made it cheaper for the Generator to declare the change than to hide it.

## Where it fell short
- **A spec assumption slipped through with every check green.** In `02/sprint-2` the escalation timing contradicted
  approved spec assumption 6, yet the score was 100. The contract never encoded the assumption, and the Evaluator
  grades against the contract (Design Brief §A). The Evaluator noticed the conflict but could only raise it as an
  advisory. The Monitor then traced it to a missing link between spec and contract.
- **The FAIL path was never exercised.** All five sprints passed on the first iteration. The retry loop,
  CONDITIONAL_PASS carry-over and escalation are evidenced only by unit tests and the routing table, not by a live
  run. The headline result is good, but it leaves the most important safety path unproven in practice.
- **Cost estimation was poorly calibrated.** The Monitor's file-size estimate came in about 3.5 times below the
  measured usage of roughly 50k to 120k tokens per invocation. Tool output and source files read more than once
  dominate, and agents do not record what they read.
- **Skill feedback depended on a human applying it.** Run-logs proposed fixes after sprint 1, but nothing changed
  until refinement 1, which I applied by hand after four sprints.

## One concrete improvement
**Add a scheduled fault-injection sprint and enforce spec traceability mechanically.** Before a team adopts the
harness, run one sprint whose contract deliberately includes a rule the Generator is known to miss. An example is
an AC requiring a 403 path that is not in the skill examples. That proves FAIL, then required fixes, then
iteration 2 on a real run, and gives the escalation output a live sample.

Separately, extend `harness-check` with a deterministic **HG-9**: every `Assumption n` and `Rule` line in the
approved `spec.md` must appear in some contract's "Spec traceability" table. This moves the lesson of run 02 from
an LLM check (B6) to a hard gate, the same principle as Design Brief decision 1: let the model judge, and let code
enforce.

<!-- FINALIZE: Cognizant Claude adds 3–5 lines here on features 03–04 run on the Cognizant workstation (iterations,
any FAIL / escalation, environment differences) and trims the text above if needed to keep this page to one page. -->
