---
name: autotest
description: Run the ValheimTomrer in-game autotest (scenarios, release check editor_all) and read its result. Use when asked to test, verify or run the release check. Needs the real game, so only on the owner's machine.
---

# Run the autotest

The autotest drives a real Valheim with its own character and world. It cannot run in a cloud session
(no game). If the game is not here, say so, give the exact command and stop.

1. Close Valheim. The script refuses to start while it runs.
2. Run one scenario, or the whole check:
   - `npm run autotest -- <scenario>` (picks `scripts/autotest.sh` or `autotest.ps1` by OS)
   - Release check: `npm run autotest -- editor_all`, about 11 minutes. Pass means `DONE pass=N fail=0` and an `art guard:` line.
   - Some scenarios only: `VT_CHAIN="editor_build,blueprints"` (PowerShell: `$env:VT_CHAIN=...`) with `editor_all`.
3. Read `.devtest/result.txt` and the `FAIL` lines of `.devtest/LogOutput.log`. Screenshots are in `.devtest/*.png`; a run deletes the old ones, so look at them right after.
4. A failure is real until proven otherwise. Reproduce it alone with its scenario before calling it flaky. Never skip or weaken a check to get green.
5. Report: scenario names, pass and fail counts, the first failing check with its message.

Scenario list and what each proves: `.claude/rules/autotest.md`, details in `.claude/design-decisions.md` §10.
