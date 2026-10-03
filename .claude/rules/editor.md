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
  fires the game's input module and ours in the same frame. Palette tiles, the "In blueprint" rows
  and the problem rows are plain Images, not Selectables.
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
  in hand, release the pane, leave the walk, clear the selection, close the window.
- **A row chip that carries text** (`item_background`) is tinted `UiTheme.Slot`, and a `Button` on
  it uses `Selectable.Transition.None`. Icon-only tiles keep the sprite as it is.
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
