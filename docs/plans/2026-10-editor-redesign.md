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
- Every feature works on a controller too (CLAUDE.md), with its autotest check and README line.

## Phases

| # | Phase | Contents | State |
|---|---|---|---|
| 1 | Theme and wheel | Own wheel scroll for every list (eased, one notch is two rows in the palette). Night theme: dark panels, dark sky, ground and grid. Top bar button, config `Editor/Theme` | pushed, to be tried |
| 2 | Viewport first | The 3D view fills the screen. Panels become small floating cards that collapse to an icon: Layers (left), Inspector (right), Checks. The top bar shrinks to one thin row. `Ctrl+\` hides all interface. Tab opens Quick add: a popup with search, categories, Recent and Favourites, over the view (it reuses the pad's piece menu). The panel walk moves from Tab to F6 (L3 on the pad stays) | next |
| 3 | Key bindings | One table of actions. Presets. `Controls` window: search, press a key to bind, conflicts shown, reset one or all. File `config/ValheimTomrer/keymap.cfg`. Pad bindings the same way | |
| 4 | Figma-style work | Selection: marquee (add with Shift), Ctrl+click in a group, Shift+click toggles. Alt+drag copies. Ctrl+G group, Ctrl+Shift+G ungroup. Layers with eye and lock. Align and distribute. Space+drag pans, Ctrl+wheel zooms. Rename, reorder | |
| 5 | Extras | Ruler and sizes in metres, mirror, array (copies by a step), command search `Ctrl+K`, recent files, material warnings on tiles | |

## Rules for every phase

- Build passes (`npm run build`), `npm run check` passes, new files are in `.claude/project-map.md`.
- Hot reload cleanup is in `OnDestroy` for anything new.
- The autotest gets a check for each new thing, and the README tables get their lines.
- Reasons go to `.claude/design-decisions.md` §3, the rule to `.claude/rules/editor.md`.
