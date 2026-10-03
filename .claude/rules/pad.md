---
paths:
  - "src/Editor/Input/**"
  - "src/Patches/ZInput*"
  - "src/Editor/Ui/PiecePicker.cs"
---

# The controller

Reasons: `.claude/design-decisions.md` §4.

Every key the mod reads with the window closed has a pad twin, read in `Input/WorldPad.cs` through
the game's own button names, so the game's layout decides. "L2" is the game's modifier `JoyAltKeys`
(L1 in the alternative layout). The player's tables are in `README.md`.

| Pad | Does | Key |
|---|---|---|
| square, hammer out | next blueprint (`BlueprintMode.Cycle`) | B |
| R2 / L2 + right stick, in blueprint mode | build / turn | click / wheel |
| R1, in Continue | Remove (the game's `JoyRemove`) | Remove |
| L2 + square | open the editor, the blueprint in hand rule included | F7 |
| L2 + square, in the editor | close it (`PadBindings.ClosePressed`); square alone still moves | F7 |
| L2 + triangle | start a capture, again: take it | F8 |
| D-pad, L2 + D-pad, circle | turn, both sides, width, depth; stop the capture | wheel, Esc |

- World combos read the game's buttons (`ZInput.instance.GetButtonDef`), never the pad itself. The
  editor reads the pad itself (`PadReader`) and maps the game's modifier with
  `WorldPad.ModifierButton`, so the same two buttons open and close it.
- They work only while `WorldPad.Live`: mod on, window closed, `EditorSession.CanOpen`, no game menu.
- A combo's buttons are held back from the game in `ZInputTryGetButtonStatePatch`, grouped by
  binding path (not by name), and stay held until let go (`WorldPad.Tick`).
- **A press that closes something must not reach the game** (circle is the jump, read in the next
  FixedUpdate): the capture holds it until let go, the remove window resets it
  (`SiteRemovePopup.SwallowPad`, through `ZInput.ResetButtonStatus`).
- **The editor's look is the game's** (`EditorCamera.TurnPad`): 110 degrees a second times
  `PlayerController.m_gamepadSens`, the game's invert settings, no setting of our own. `PadReader`
  reads the sticks with `ReadUnprocessedValue`, never `ReadValue()`: the game sets Unity's stick
  filter to 0.4 to 0.75, which made the look clunky (§4).
- **In the editor the pad works off the crosshair.** Square moves, triangle copies, R1 deletes, each
  on its own, all through `PadBindings.TakeAimed`. Keep the three the same.
