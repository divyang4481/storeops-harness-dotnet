Prepare the capstone submission from this repository. Work through every step; report a checklist at the end.

1. **Completeness check** against SUBMISSION.md "Rubric → where the evidence is". For every feature folder in
   `.harness/reviews/` confirm: spec.md (STATUS: APPROVED), and for each sprint N: sprint-N-contract.md,
   sprint-N-generator-summary.md, sprint-N-evaluator-feedback.md (ends with a ```verdict block), sprint-N-checks.md,
   sprint-N-run-log.md; and one INDEX.md row per sprint. List anything missing — do not fabricate artefacts.
2. **Gate:** run `powershell -ExecutionPolicy Bypass -File scripts\harness-check.ps1` — it must print `HARD_GATES: PASS`.
   Then `scripts\verify.ps1` must print `ALL CHECKS PASSED`. If not, stop and report.
3. **Fill the `<!-- FINALIZE ... -->` sections** using only facts from `.harness/reviews/` (run-logs, INDEX.md),
   `docs/screenshots/` and git log:
   - DEPLOYMENT.md: link the screenshots that exist in `docs/screenshots/`; AWS URL only if the developer gives it.
   - DESIGN_BRIEF.md: if features 03–04 exercised a FAIL iteration, CONDITIONAL_PASS or escalation, add a 2–3 line
     note in Section C quoting that run's evaluator feedback (file + check id). Keep the brief within 5 pages.
   - REFLECTION.md: complete the Cognizant section (max 1 page in total — shorten elsewhere if needed).
   - JOURNAL.md: add entries 11+ for features 03–04 (environment differences, iterations, notable evaluator findings).
   - SUBMISSION.md online text: fill the feature 03/04 results.
   Ask the developer for their full name and replace `<your full name>` in DESIGN_BRIEF.md.
4. **Commit locally** (do NOT push): `git add -A` then `git commit -m "submission: finalize capstone artefacts"`.
5. **Build the zip:** `git archive --format=zip -o ..\storeops-harness-submission.zip HEAD` and report its path and size
   (must be under 128 MB).
6. Print the completed Online text from SUBMISSION.md for the developer to paste into Moodle.
