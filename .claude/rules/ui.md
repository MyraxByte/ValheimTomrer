---
paths:
  - "src/Editor/Ui/**"
  - "src/Blueprints/HintRow.cs"
  - "src/Blueprints/BlueprintInfoCard.cs"
---

# Custom UI

For any UI task (HUD text, messages, hover text, map pins, popups, own windows, menu buttons),
**read the research before writing code** (checked against game 1.0.15):

| File | Read it for |
|---|---|
| `.claude/research/19-09-2026-custom-ui.md` | Start here. Which approach fits which need, own-window checklist, open scope questions |
| `.claude/research/19-09-2026-custom-ui-game-api.md` | Exact game methods, patch targets and code for every UI surface; fonts and sprite names |
| `.claude/research/19-09-2026-custom-ui-platform-and-mods.md` | Input, UI systems, asset bundles on macOS, how other mods do it, links |

- Build with uGUI + TextMeshPro in code. No `UnityEngine.UI.Text`, IMGUI only for debug.
- A new TMP text shows nothing until `.font` is set. Copy it from a vanilla text.
- **Do not share the vanilla text material**: at 12 to 16 point every label on it reads grey (§2).
  Use `UiTheme.FontMaterial` for text on a panel, `UiTheme.FontOutlined` for text over the 3D
  picture (`UiBuild.OverPicture`).
- An own window needs its own `Canvas` + `CanvasScaler` (reference pixels per unit 50) +
  `GuiScaler`, and the input-blocking patches listed in the research.
- Never use the UI calls that send network messages (listed in the research, e.g.
  `Chat.SendText`, `DamageText.ShowText`, map pins with `save: true`).

---

## The build card and the controls row

Layout, numbers and the full sets: `.claude/design-decisions.md` §8, §9.

Layout, numbers and the full sets: §8, §9.

- **The card** (`BlueprintInfoCard`). The game sets its six slots every frame, so the postfix only
  hides the list and there is nothing to undo. The list never grows up over the card (the game puts
  the stamina and eitr bars there in build mode). No "From:" line and no controls on the card: the
  user took both out.
- **A plan over 5 ms** waits until the preview holds still for one refresh, so moving a big blueprint
  never stutters.
- **The controls row** (`HintRow`). Every entry is a copy of the game's own (`Place`, `key_bkg`,
  `+`, `mousew_icon`, `Text - Place`), never a look-alike. Pad icons come from
  `Localization.GetBoundKeyString`, keys from the game's names and `ZInput.KeyCodeToDisplayName`, so
  both follow the player's layout. When the mode ends the game's own update runs again: nothing to
  undo.
- A control of blueprint mode, Continue or the capture is listed in `HintRow` (keyboard and pad),
  never on the card or the capture's status line.
