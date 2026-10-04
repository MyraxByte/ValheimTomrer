---
paths:
  - "src/Editor/**"
  - "src/Patches/EditorInputBlockPatches.cs"
  - "src/Patches/BuildUiOnLayoutChangedPatch.cs"
---

# The editor (F7)

Read `.claude/handoff/editor.md` and `editor-refine.md` first if they exist (local files). Reasons and full behaviour: `.claude/design-decisions.md` §3.

F7 opens a window with a 3D view, the piece list and the game's snapping.

- **Layer 30.** Everything the editor draws is on layer 30, 8000 m under the world
  (`EditorScene.Depth`), and every physics query it makes is masked to layer 30.
- **Input.** While the window is up, `EditorInputBlockPatches.cs` holds the game's input, every
  patch gated on `ModUi.Blocking`.
- **The mouse.** The cursor is free the whole time. Only C gives it to the pane
  (`ViewportHost.Capture()`), Esc gives it back. A click must never call `Capture()`. There are no
  camera modes, the camera always flies. Its look is the game's (`EditorCamera.MouseLook`): 0.05
  degrees a pixel times `PlayerController.m_mouseSens`, the game's invert, no setting of our own (§3).
- **The hint bar.** `ViewportHost.ShowHints` runs every frame: the bar rebuilds only when its set
  changes. Its words are `UiTheme.TextOnPicture` on `UiTheme.FontMaterial`, no edge, no shadow (the
  user had a white edge taken out).
- **The panel walk** (`FocusNav`) keeps the EventSystem's selection null for buttons, or a real pad
  fires the game's input module and ours in the same frame. Quick add tiles, the Layers rows and
  the Checks rows are plain Images, not Selectables.
- **A new dialog** ends its builder with `Start()` (it sets `Dialogs.FocusStart`), so the walk can
  enter it. `Dialogs.Tick` stands back on Enter while the walk is in the dialog, or one press fires
  twice.
- **Text boxes** are `UiBuild.InputField` (a `TextBox`), never a plain `TMP_InputField`: the game's UI
  sends the real pad's cross, circle and D-pad to a typing box, and a plain one saved Save as on cross.
  On the pad, circle or a D-pad step leaves a typing box; Save as starts typing only when
  `EditorInput.PadInUse` is false. Reasons: §3.
- **The Esc and circle ladder**, in this order: a text box gives the keyboard back
  (`EditorSession.Tick`, read through `ModUi.JustTyping`, so an Esc the box took first this frame
  cannot also close the dialog), then the dialog, the piece menu, what is
  in hand, release the pane, leave the walk, clear the selection, bring a hidden interface back,
  close the window. The header's menu and Quick add close before what is in hand.
- **The look is a design tool's, not the game's.** Panels are built from `Kit` (ghost, solid and
  primary buttons, rows, columns, fields with the caption inside, tabs, segmented switches) on flat
  `UiTheme` colours; no game sprite. A widget inside the game's own HUD (the hammer card's materials
  list, the capture's status line) builds and refreshes inside `using (UiTheme.GameLook())`.
- **No glyph a plain font may lack.** The sans font may have no star or arrow symbols: mark with a
  shape (the Starred dot) or words.
- **Cancelling a dialog goes through `Dialogs.Dismiss`** (Esc, circle, the X, the backdrop, Cancel),
  so a question asked from the Blueprints list goes back to it. `Close()` alone skips that.
- **An empty blueprint is a real file.** `BlueprintFormat` and `DocumentStore` read and write one,
  `BlueprintLibrary.TryAdd` keeps it out of `All`, the problem list calls it a warning.
- **Closing keeps everything** (the document and its undo, the selection, the hand, the camera, the
  tabs and filters, a dialog, the walk); the scene sleeps (`ViewportHost.Sleep` / `Wake`). Only the
  mouse is not kept. `EditorSession.Forget()` is the full wipe.
- **The hand wins.** F7 with another blueprint in the build tool opens that one through
  `EditorCommands.Take`, which asks first over unsaved changes. The world capture opens the same
  way. The same file in hand comes back to the kept session.
- **A prefab copy is made with no parent**, then moved under its parent (`BlueprintPreview.Build`).
  Under a switched-off parent it becomes a real world piece 8000 m down, and the ghost vanishes two
  frames later.
- **Support** (`Placement/Support.cs`): the material numbers come from the game's own
  `GetMaterialProperties`, never typed in. The model may refuse what the game would allow, never the
  other way round. `Evaluate` takes the ground callback from the map, so always pass it a real map.
- **The materials list** in the editor: `MaterialSources.Around` at most once a second while the
  window is open, never while it is closed.
- **The screen** (`EditorWindow`): a root canvas of its own, never a child of the HUD (the HUD's
  canvas is larger than the screen and cut the old window off). The 3D view is the whole screen and
  never changes size; the interface is islands over it (opaque rounded cards with a margin and a
  shadow, `UiBuild.Card`): three header islands, the Layers card, the Inspector card, the toolbar.
  A card that is put away slides off screen (`EditorWindow.ApplyLayout`, from offsets only, so it
  can run twice) and an edge tab brings it back; it is never switched off, because panels measure
  text while they build and tick and TextMeshPro measures nothing in an inactive object. The same
  reason: the root is switched on before it is built. The walk skips a put-away card.
- **Text over the view goes in the free room** (`EditorWindow.FreeLeft/FreeRight/FreeTop/FreeBottom`),
  set by `ViewportHost.Relayout`, never against the screen's edge. Anything that sits over the picture
  hangs on the root after the view, never inside it: the view's picture draws over its own children.
- **Button widths come from the words at layout time** (`LabelWidth`), never from a
  `GetPreferredValues` call while building: that measures 0 in an inactive object and the label clips.
- **The 3D tools**: view presets (`EditorCamera.SetView`, keys 1 to 5, the gizmo `ViewGizmo`), orbit
  (`EditorCamera.Orbit`, Alt + right drag, pad L1 + right stick), orthographic (`ToggleOrtho`), Isolate
  (`EditorState.IsolateSelection`). The pad twins are L2 + D-pad. A new camera tool needs all three:
  a keymap act, a pad twin and a line in the help.
- **The look is dark only.** There is no light theme (the owner dropped it). The 3D scene is dark and matte on
  purpose: the ground has no shine, the sky is not reflected (`PreviewCamera.Render`), the grid has every fifth
  line brighter. The ambient is sky, horizon and ground (Trilight), not one flat colour. A bright scene hid the grid.
- **Session-only, never in the file**: hide, lock, isolate, groups, the clipboard, saved views, the ruler.
  Groups live in `EditorState` (`Select` expands a piece to its group). Anything that must be in the file
  needs a format change in `Blueprint.cs` / `BlueprintFormat.cs`, which the desktop Tomrer shares: ask first.
- **Over the picture**: sizes, gaps and the ruler are `Annotations` (UI lines and numbers), filled each frame by
  `ViewportHost.DrawAnnotations` from cached numbers (`EditorMeasure`). Never a mesh or a texture.
- **One throw must not trap the player**: `EditorSession.Tick` runs every panel's update inside `Safe`; the Esc
  and close code runs outside it. A new per-frame panel update goes in through `Safe`.
- **Undo runs** of one tag (nudge, a typed number) end after 1.2 s, on a new selection and on save; an edit
  that changes nothing makes no step. Align, mirror and spread are one step each.
- **The light** is `View/SceneLook.cs`: four looks by time of day, all colours from code. A new colour of the
  scene goes into the `Look`, never as a constant in `EditorScene`.
- **Lights of pieces work in the editor** (`BlueprintPreview.LightUp`): a torch keeps its `Light` (editor layer
  only, soft shadows so walls block it) and flickers; `LightLod` is always removed. The aim preview has no lights.
- **Text boxes draw their own cursor and selection** (`FieldLook.DrawCaret`) because TextMeshPro's were not
  visible in this window. Do not remove them without seeing the game's ones work.
- **Inspector tabs** slide too (`Inspector.SetTab`); only Quick add switches its popup off, and it
  builds its tiles and chips in `Open`, after switching on.
- **Quick add** (`QuickAdd`) is the only piece picker: Tab, the pad's cross and the Add mode open it.
  While its search box types, `QuickAdd.Tick` reads Tab, Esc, up and down itself.
- **Keys come from the keymap** (`Input/Keymap.cs`). A new action is an `Act`, a `Def` row with the
  three presets' keys, and a case in `Bindings.Run`. Esc, Enter and the arrows inside a dialog or the
  walk are not in the table. Tab is Quick add, F6 the walk.
- **Hidden and locked pieces** are this session's only (`EditorState.Hidden`, `Locked`), never in the
  file. `Select` and `SelectAll` skip them; the pane does not draw a hidden one (`NotDrawn`).

