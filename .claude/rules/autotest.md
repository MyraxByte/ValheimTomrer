---
paths:
  - "src/Dev/**"
  - "scripts/autotest*"
---

# The autotest

Scenario table and details: `.claude/design-decisions.md` §10.

`npm run autotest -- <scenario>` (`scripts/autotest.sh` on macOS, `autotest.ps1` on Windows), Debug builds only. A new scenario goes in both scripts' lists. The scripts need the real game, so they run on the owner's machine, never in a cloud session.

| Scenario | What it proves |
|---|---|
| `probe`, `dump` | measures layers, UI, input and pieces into `.devtest/*.txt`. No feature |
| `probe_build` | measures chests, support speed, ground, glow and the card. Not in `editor_all` |
| `build_sources` | materials from the bag and chests: order, exact amounts, range, access |
| `build_partial` | a click builds what is paid for and would stand, bottom to top |
| `build_sites` | unfinished builds: the file, the ghosts and their colours, finishing one |
| `build_continue` | Continue, Remove and the remove window, keyboard and pad |
| `card_materials` | the hammer card's materials list |
| `hint_row` | the controls in the game's hint row, keyboard and pad |
| `blueprints` | every shipped kit built with the hammer, B and square cycle the same |
| `editor_open` | F7 and L2 + square open and close the window |
| `editor_view` | the 3D pane and its camera, nothing leaks on close |
| `editor_files` | the writer round-trips every blueprint, the file commands |
| `editor_snap` | placing and snapping against a table of rays, no UI |
| `editor_edit` | place, select, copy, turn, nudge, undo |
| `editor_keys` | every key, the wheel, the mouse, the top bar, the dialogs, deleting a blueprint |
| `editor_pad` | every pad button through a made-up pad, the piece menu, the look against the game's |
| `editor_keep` | close and open again finds everything as it was |
| `editor_build` | a blueprint made in the editor, built in the world, edited again |
| `editor_capture` | the capture, keyboard and pad |
| `editor_support` | the support rule against the game's numbers. `VT_SUPPORT_EDITOR_ONLY=1` skips the world half |
| `editor_ui` | the editor screen: the window is the screen, the view between the panels, Alt+1 and Alt+2 fold them, Ctrl+\ and L2 + L3 hide all, the pad walk reaches header, Layers, Inspector and toolbar, the menu, the Inspector tabs, Layers groups, Quick add (Tab, search, Recent, Starred, keyboard and pad), the wheel step, Dark and Light, the keymap and presets, the Keys window and the command search, hide and lock, align, spread, mirror, copy in a row, Shift + arrow, grid and turn step |
| `editor_all` | all of the above except `probe_build`, then the art guard. **This is the one to run.** |
| `readme_gifs` | no test: records the README's GIF frames. Not in `editor_all`. Then `scripts/make-gifs.py` |

- **The release check** is `editor_all`: `DONE pass=N fail=0` plus `art guard:` in the output. It
  takes about 11 minutes. `VT_CHAIN="editor_build,blueprints" ./scripts/autotest.sh editor_all` runs
  only those two, in that order.
- Every run of `autotest.sh` deletes `.devtest/*.png` first. To look at one scenario's screenshots,
  run it alone or last.
- Every world-building scenario builds at `AutoTest.MoveToBuildSpot` (levelled flat, §10), and
  `AutoTestPeace` quiets the world at the start of every scenario.
- `AutoTest.Reset` runs between scenarios: the editor forgotten, every unfinished build and
  `.devtest/sites` gone, every setting back to its default (`AutoTest.ResetSettings`), so no run
  changes the player's config file.
- **Not covered:** a real pad pressing cross while the panel walk is on (the fake pad is invisible to
  `InputSystemUIInputModule`). Check it by hand with a pad plugged in.
