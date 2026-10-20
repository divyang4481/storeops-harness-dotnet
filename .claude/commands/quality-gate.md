Run the full StoreOps quality gate and fix anything that fails.

1. Run `powershell -ExecutionPolicy Bypass -File scripts\verify.ps1` on Windows (or `./scripts/verify.sh` elsewhere).
2. If a step fails, read the error, fix the root cause following CLAUDE.md (never silence analyzers without a commented reason), and re-run the script.
3. Repeat until it prints `ALL CHECKS PASSED`, then summarise what you changed (files + reason) in a short list.
