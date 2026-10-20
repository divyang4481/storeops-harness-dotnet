Report the current StoreOps harness state and resume if appropriate.

1. Read `.harness/output/progress.md` (if missing: say no run is active and list `.harness/reviews/INDEX.md`).
2. Report FEATURE, STATUS, SPRINT, ITERATION, BASE_REF and the last 5 HISTORY lines.
3. If STATUS is IN_SPRINT, continue the sprint loop from the recorded step (CLAUDE.md §3 step 4).
   If AWAITING_APPROVAL, remind the developer to review spec.md and type APPROVED. If ESCALATED, show escalation.md.
