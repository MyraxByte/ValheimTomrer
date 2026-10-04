# Editor redesign: plan

Asked by the owner on 2026-10-03. Decisions made with the owner are marked **Decided**.

## Goals

1. The editor (the 3D view) is the main thing. The interface helps and stays out of the way.
2. The look fits Valheim (the game's wood, gold, hover text), with a Night theme.
3. Working with a blueprint feels like Figma.
4. Key bindings are flexible and easy to change.
5. Tab opens the list of pieces to add (replaces the Pieces tab).

## Decided

- All phases, in order. Each phase is pushed on its own, the owner tries it, then the next starts.
- Key bindings: presets (Figma, Valheim, Blender) plus own bindings, with a Controls window.
- Moving pieces stays as it is now: G, the piece follows the mouse, click drops. No axis handles.
  Dragging a piece moves it too (Figma), G is unchanged.
- The look does not follow the game: a modern flat tool look, dark only (the owner dropped the Light theme)
  (changed by the owner during phase 4).
- "Привязки" covers both: key bindings (phase 3) and snapping (phase 6).
- Every feature works on a controller too (CLAUDE.md), with its autotest check and README line.

## Phases

| # | Phase | Contents | State |
|---|---|---|---|
| 1 | Theme and wheel | Own wheel scroll for every list. A dark theme (Light was added, then removed) | done, not run in the game |
| 2 | Viewport first | The view fills the screen, Layers and Inspector cards fold (Alt+1, Alt+2), `Ctrl+\` / L2 + L3 hide everything, Tab opens Quick add (search, Recent, Starred), the walk moves to F6 | done, not run in the game |
| 3 | Key bindings | One table of actions, presets Tomrer, Figma, Blender, the Keys window (H, Change keys), config section `Keys`. The pad keeps the game's layout (decided: no pad rebinding) | done, not run in the game |
| 4 | Figma-style work | Drag to move, Alt+drag copies, double click selects the same kind, hide and lock (keys, Selection card, Layers rows), align and spread, Shift + arrow. Modern flat look. Groups are left out: the file format has none | done, not run in the game |
| 5 | Extras | Command search Ctrl+K, mirror X and Z, copy in a row, sizes in metres | done, not run in the game |
| 6 | Snapping | Grid 0.25 to 4 m, turn step 5 to 90 degrees, snap points on and off, the snap bar | done, not run in the game |

## Phase 7: the editor screen from scratch (owner's request after phase 6)

Mockup approved by the owner: https://claude.ai/artifact/9DauTNnkyk49g1KXuqLhKm. One menu button
instead of desktop menus (the owner's note: it runs inside a game).

- Own root canvas, exactly the screen (the old window was cut off at the edges).
- Header: menu, blueprint name and state, modes (Select, Add, Move, Copy), undo, redo, commands,
  theme, help, Build in world.
- Layers docked left, grouped by kind, Hide and Lock on hover. Inspector docked right with Design,
  Blueprint and Checks tabs. The view between them; a floating toolbar for snapping and view.
- Quick add rewritten as the only piece picker, search first, keyboard, mouse and pad.
- Old panels removed: TopBar, PieceListPanel, SelectionPanel, BlueprintPanel, ChecksPanel, Palette,
  PiecePicker. Their scenarios (editor_palette, editor_panels, editor_focus) were removed; editor_ui
  covers the new screen.

## Phase 8: islands, a full-screen view, 3D tools (owner's feedback on phase 7)

The owner's four points: opaque panels as islands that can be put away, the space on the whole
screen, stable positioning, better work in 3D.

- Found while reading the code: the toolbar and the status line were children of the view, drawn under
  its picture; button widths were measured while the window was switched off (TextMeshPro measures 0,
  so labels clipped); the docks mixed `anchoredPosition` and offsets. All three fixed.
- The view is the whole screen and never resizes. Islands: three header islands (file, modes,
  actions), the Layers and Inspector cards (Hide button, edge tab to bring one back), the toolbar.
  Opaque, rounded (runtime sprite, never saved), shadowed. Text over the view stays in the free room.
- 3D: views 1 to 5 and the gizmo (front, right, top, back, left, bottom, corner; perspective or
  orthographic), orbit round the selection (Alt + right drag, L1 + right stick), the selection's size in
  the status line, Isolate (I). Pad twins: L2 + D-pad.
- Not done on purpose: a ruler tool and lens shift for the free room (the crosshair stays at the screen's middle).

## Left for the first run in the game

- `npm run build`, then `npm run autotest -- editor_ui`, then `editor_all`.
- Some older scenarios still count widgets of the old layout (`editor_keys`, `editor_pad`,
  `editor_keep`). Expect FAIL lines there; they get fixed from the run's output.

## Rules for every phase

- Build passes (`npm run build`), `npm run check` passes, new files are in `.claude/project-map.md`.
- Hot reload cleanup is in `OnDestroy` for anything new.
- The autotest gets a check for each new thing, and the README tables get their lines.
- Reasons go to `.claude/design-decisions.md` §3, the rule to `.claude/rules/editor.md`.
