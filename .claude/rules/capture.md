---
paths:
  - "src/Editor/WorldCapture.cs"
  - "src/Editor/View/Capture*.cs"
---

# The capture (F8)

Reasons: `.claude/design-decisions.md` §5.

F8 (`Editor/CaptureKey`) or L2 + triangle, with the window closed, in `WorldCapture.cs`. The rules
for what it takes, its sizes and its glow are in the design notes.

- It keeps `ModUi.Blocking` false, so the game keeps its input. The wheel patch reads 0 while
  `WorldCapture.Active`, and `Menu.Update` skips the frame the capture takes Esc
  (`WorldCapture.TakesEscape`), or Esc also opens the pause menu.
- Every way out calls `CaptureTint.Clear`: capture, Esc, mod off, window open, world change. A house
  that stays yellow or orange means one path missed it.
- Save as opens only once the captured blueprint is the open one: `OpenDocument(doc, opened)` and
  `EditorCommands.Take(..., taken)` run it after the "Discard?" question, never instead of it.
- The status line (`CaptureHud`) has no controls, and no message comes up when a capture starts.
