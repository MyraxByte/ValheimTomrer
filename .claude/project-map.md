# Project map: every file

Nothing here is a guess. Use it instead of searching. Every file outside `bin/`, `obj/` and `.devtest/`. Add or remove a file, update this list in the same commit.

Nothing here is a guess. Use this instead of searching. All 88 source files, and every other
**Build, scripts, data**

```
ValheimTomrer.csproj              netstandard2.1, local refs, publicizer, embeds the kits, the AI
                                  metadata Thunderstore asks for, auto-deploys the DLL into
                                  BepInEx/plugins after every build (-p:DeployToGame=false skips it)
Directory.Build.props             finds the install on Windows, macOS and Linux (VALHEIM_INSTALL, or Directory.Build.props.user)
README.md                         the player-facing readme, also the Thunderstore page: features,
                                  install, keys, the controller tables. Written to read human (no
                                  bold lead-ins, no fragment lists), images by GitHub raw URL
CHANGELOG.md                      the player-facing changes, one block per released version
LICENSE                           MIT
package.json                      npm run build, build:release, hot (watch), dev, dev:hot, autotest, check, zip
.gitignore                        ignores bin, obj, .devtest, and the local .claude/ state (plans, handoff,
                                  research, settings.local.json)
scripts/dev.sh | dev.ps1          build, deploy, launch, tail our log lines (--debug adds the debugger, --hot
                                  deploys for ScriptEngine). .sh for macOS, .ps1 for Windows
scripts/run.mjs                   one entry for npm: runs the .ps1 on Windows and the .sh elsewhere
scripts/check-rules.mjs           the world-save and art checks, every OS. npm run check, and the hook
docs/development.md               how to build, run, hot reload and debug, Windows and macOS
docs/windows-setup.ru.md          step by step setup on Windows, in Russian, for the project owner
.claude/settings.json             shared permissions and the rules hook
.claude/rules/*.md                rules per area, loaded only when Claude reads matching files
.claude/skills/*/SKILL.md         workflows: autotest, release, game-update
.claude/agents/game-code-reader.md  subagent that reads decompiled game code
scripts/autotest.sh | autotest.ps1  run one AutoTest scenario, output lands in .devtest/
scripts/zip.sh | zip.ps1          npm run zip: Release build (not deployed), checks the manifest,
                                  one version everywhere and the icon, then
                                  thunderstore/build/ValheimTomrer.zip, made from scratch
scripts/make-gifs.py              the readme_gifs frames in .devtest/gifs/ to docs/media/*.gif
docs/media/*.gif                  the README's GIFs: editor, build, capture
blueprints/workshop.blueprint     the only shipped kit, embedded in the DLL as ValheimTomrer.Kits.*
tests/fixtures/                   workshop.canonical.blueprint, exactly what the writer must
                                  produce. Embedded in Debug only, the autotest reads it in game
thunderstore/manifest.json        the package's manifest (description 250 characters at most)
thunderstore/icon.png             the package icon, 256x256. The one image the art guard allows
.vscode/tasks.json                tasks "build" and "run: game (debug)"
.vscode/launch.json               "Attach to Valheim", vstuc, 127.0.0.1:10000
```

**Docs in `.claude/`** (read these before touching the area they cover)

```
design-decisions.md               the reasons, the numbers and the full behaviour behind every
                                  rule here, one numbered section per area. Tracked in git
handoff/editor.md                 the editor's build record, phases 0 to 12. Read before any
                                  editor change: it says why things are the way they are
handoff/editor-refine.md          the second pass on top of that: readability, one camera, the
                                  panel walk, the hint bar. Newer than editor.md, it wins
handoff/build-sites.md            the log of the capture and partial-build plan, one block per
                                  phase: files, measured numbers, gotchas
plans/20-09-2026-in-game-editor-done.md      the editor plan that was run. Done
plans/20-09-2026-editor-refine-done.md       the second pass on it. Done, with two decisions
                                             overturned in use. Read its Done block first
plans/19-09-2026-blueprints-research-done.md what the blueprint formats are. Done
plans/21-09-2026-capture-and-partial-build-done.md  Homestead-style capture, building from
                                             chests, part builds, the materials list. Done (Phase 8,
                                             ItemDrawers, skipped). Read its Done block first
research/19-09-2026-custom-ui.md             UI: start here, which approach fits which need
research/19-09-2026-custom-ui-game-api.md    UI: exact game methods, patch targets, fonts, sprites
research/19-09-2026-custom-ui-platform-and-mods.md  UI: input, asset bundles on macOS, other mods
```

**`src/`, the mod**

```
Plugin.cs                         BepInEx entry. Binds ModEnabled and BlueprintKey, starts
                                  Harmony, and its Update drives EditorSession.Tick and SiteTracker.Tick
```

**`src/Patches/`**, one class per target, file named `<Type><Method>Patch.cs`

```
BuildUiOnLayoutChangedPatch.cs    while the editor is up, the build menu no longer clears the UI
                                  selection when the player switches keyboard and pad (it threw a
                                  typing box out of the keyboard)
EditorInputBlockPatches.cs        the whole input takeover, nine patches in one file on purpose,
                                  all gated on ModUi.Blocking
EnvManAwakePatch.cs               keeps the world's sun out of the editor's scene
FejdStartupAwakePatch.cs          main-menu smoke test, prints harmony=OK | publicizer=OK
GameCameraAwakePatch.cs           clears layer 30 from the player camera's culling mask
HudSetupPieceInfoPatch.cs         the vanilla build card shows the blueprint and its materials list,
                                  not the piece. Anything else hides the list
KeyHintsUpdateHintsPatch.cs       skips the game's hint row update while HintRow shows a set
                                  (blueprint mode, Continue, a capture)
PlayerCanRotatePiecePatch.cs      stops the wheel zooming the camera in blueprint mode
PlayerInRepairModePatch.cs        same thing, the repair-mode branch of the camera zoom
PlayerSetSelectedPiecePatch.cs    picking any piece leaves blueprint mode
PlayerUpdateAvailablePiecesListPatch.cs  unlocks changed, flag the catalog to read them again
PlayerUpdatePlacementGhostPatch.cs       show the blueprint preview instead of one piece
PlayerUpdatePlacementPatch.cs     build-mode input: the blueprint key (or square on the pad), the
                                  click, the wheel
ZInputTryGetButtonStatePatch.cs   holds back from the game the pad buttons the world combos use
                                  (WorldPad): the D-pad and circle during a capture, square and
                                  triangle while L2 is held. Every GetButton* of the game comes here
```

**`src/Blueprints/`**, the file format and the hammer

```
Blueprint.cs                      the data: Blueprint + BlueprintPiece. Keep free of other game
                                  code, Tomrer compiles this file on its own
BlueprintFormat.cs                reads and writes .blueprint, reads .vbuild. Same rule as above
BlueprintLibrary.cs               kits from the DLL plus the player's files in
                                  BepInEx/config/ValheimTomrer/blueprints
ResolvedBlueprint.cs              a blueprint matched to live prefabs, total cost worked out once
BlueprintRules.cs                 the hammer's rules for a whole blueprint: unlocked, station in
                                  range (materials never refuse), and paying for one piece
BuildConfig.cs                    the build settings: UseChests, ChestRange. Bound in Plugin.Awake
MaterialSources.cs                where materials come from: the inventory, then every chest (cart,
                                  ship hold) in range that the player may open, nearest first
PartialBuild.cs                   which parts a click builds: paid for, station near, would stand,
                                  bottom to top. Plan, Without (a plan minus parts someone stands
                                  in), and Missing for the message
MaterialTally.cs                  the numbers of a materials list (have and need per item, a row
                                  per station, the counts), from a resolved blueprint or a plain
                                  list of pieces (the editor). No preview needed
BlueprintPreview.cs               the prefab copies. Three users: the see-through hammer preview,
                                  an unfinished build's ghost (SetPart: hidden, light blue, red) and
                                  the editor's solid model. No copy is ever a crafting station
BlueprintInfoCard.cs              fills the vanilla build card: name, icon, piece count, and the
                                  materials list on the card's right in place of the six slots.
                                  Refreshes every 0.5 s and right after a click
HintRow.cs                        the controls of blueprint mode, Continue and the capture in the
                                  game's own hint row, keyboard and pad, made of copies of the
                                  game's own entries. The sets are in Hints
BlueprintMode.cs                  blueprint mode of the hammer: the key's cycle (unfinished builds
                                  first, as Continue), the turn, the click, Continue, Remove

Sites/Site.cs                     one unfinished build: its own copy of the blueprint, the root
                                  pose, the file, and the built flags (read from the world)
Sites/SiteStore.cs                the per-world folder config/ValheimTomrer/sites/<world>_<uid>:
                                  Load, Add, Save, Delete. #Site: and #SiteSource: headers.
                                  RootOverride for the tests
Sites/SiteTracker.cs              once a second: what is built, finished builds, the ghosts with
                                  the hammer out within 64 m, and the next click's parts (cached).
                                  BuiltPieces: the world piece of each built part
Sites/SiteRemovePopup.cs          the remove window: the game's own popup (UnifiedPopup) with
                                  three copies of its No button: Cancel, Unbuilt parts, Whole
                                  structure. Mouse, Esc, and the pad (D-pad, cross, circle)
Sites/SiteRemoval.cs              "Whole structure": takes a site's built pieces down top to bottom
                                  the way the hammer's Remove does, keeps what a refused piece
                                  needs to stand, and the message
```

**`src/Editor/`**, the in-game editor (its own section below for the rules)

```
EditorConfig.cs                   every editor setting, and the layer number
PieceMemory.cs                    the pieces used last and the starred ones, kept in the config as prefab names
EditorSession.cs                  open, close (keeps everything), Forget, the one per-frame tick
EditorState.cs                    selection, what is in hand, every action that changes the
                                  blueprint. No UI, no scene, the autotest drives it alone
EditorCommands.cs                 the top-bar verbs. Every one reports through a toast
WorldCapture.cs                   F8: the turned rectangle under the aim, the wheel, and the
                                  blueprint it makes
Checks.cs                         everything that can be wrong with a blueprint, worst first
BlueprintCard.cs                  what the game's build card would show for the open document:
                                  name, icon, text, and the materials list's numbers (Materials)

Catalog/PieceCatalog.cs           all 398 hammer pieces read once per world: All, Unlocked, Visible
Catalog/PieceEntry.cs             one piece as the editor needs it: cost, size, colliders,
                                  snap points, icon, its WearNTear support numbers

Doc/BlueprintDocument.cs          the open blueprint with undo and redo. Never changed in place
Doc/DocumentStore.cs              open, save, save as, rename, delete. Temp file, then rename

Input/EditorInput.cs              raw layer: keys through ZInput, the pad read once a frame
Input/Bindings.cs                 the one keyboard dispatcher (Press, Run), the help table read from the keymap
Input/Keymap.cs                   every action, its keys per preset (Tomrer, Figma, Blender) and the Keys config section
Input/PadBindings.cs              the one pad dispatcher: dialog, then piece menu, then editor
Input/PadReader.cs                reads the first pad, dead zones, and Fake for the autotest
Input/Glyphs.cs                   button names for on-screen hints, PlayStation or Xbox wording
Input/Repeater.cs                 a held direction that repeats
Input/WorldPad.cs                 the pad in the world, through the game's own button names: square
                                  (next blueprint), L2 + square (editor), L2 + triangle, the D-pad
                                  and circle (capture), which buttons the game must not see

Placement/Placement.cs            Placer, the game's placing rule in plain C#: ray in, landing
                                  spot and snap out. No GameObject, no physics call
Placement/SceneIndex.cs           the pieces that stay put, in world space, with a 0.5 m hash
Placement/MovingSet.cs            what is in hand, laid out around a pivot
Placement/Shapes.cs               colliders as plain structs, and the ray tests on them
Placement/Support.cs              the game's support rule in plain C#: how well each piece is
                                  held, which ones would fall, and the hammer's colours. The
                                  ground is y = 0, or the terrain through a callback

Ui/EditorWindow.cs                the screen: a root canvas of its own (exactly the screen) with the 3D
                                  view over all of it and the interface as opaque islands over the
                                  view: header islands, the Layers and Inspector cards, the toolbar,
                                  edge tabs for a card that is put away. The free room (FreeLeft,
                                  FreeRight...) is where text over the view goes. Ctrl+\ hides all
Ui/Kit.cs                         the widget kit every panel uses: text, ghost, solid and primary
                                  buttons, rows, columns, dividers, inline fields, tabs, segmented
                                  switches, chips, HoverEvents and ClickEvents
Ui/Header.cs                      the three top islands: the menu button and its menu, the blueprint
                                  name and state; the modes (Select, Add, Move, Copy); undo, redo,
                                  commands, theme, help and Build in world
Ui/LayersPanel.cs                 the left panel: the blueprint's pieces grouped by kind, folding
                                  groups, search, Hide and Lock on hover. Virtual rows
Ui/Inspector.cs                   the right panel: the Design, Blueprint and Checks tabs
Ui/DesignPage.cs                  the Design tab: the selection and its size, x y z and yaw, edit,
                                  align and spread, arrange, view
Ui/BlueprintPage.cs               the Blueprint tab: name, description, icon, the build card preview
                                  and the materials list
Ui/ChecksPage.cs                  the Checks tab: the problem list, a click selects its pieces
Ui/Toolbar.cs                     the floating bar at the bottom of the view: grid, turn step, snap
                                  points, dots, boxes, hide panels
Ui/ViewGizmo.cs                   the axis marker in the view's corner: six discs that follow the
                                  camera and click to a side, the side's name, perspective switch
Ui/QuickAdd.cs                    Tab, cross or the Add mode: the piece search over the view. Typing
                                  filters, Enter places, arrows and the D-pad move, L1 R1 change tags,
                                  Recent and Starred
Ui/ModUi.cs                       the Blocking flag every input patch reads
Ui/UiTheme.cs                     the editor's flat modern look, Dark (default) or Light: colours,
                                  a sans font the game ships, own copies of its material. GameLook()
                                  gives the game's colours and font to widgets inside the game's
                                  HUD (the hammer card's list, the capture line). Keyed on the Hud
Ui/UiBuild.cs                     the lowest builders: TMP text, flat cards with a border, buttons,
                                  OverPicture for text on the 3D pane, TextBox (ignores the game
                                  UI's pad Submit/Cancel/Move), WheelScroll (the wheel in lists)
Ui/PadGlyphs.cs                   the game's own controller icons, out of its gamepad_glyphs TMP
                                  sprite asset, handed out as Sprites
Ui/HintBar.cs                     the row of controls over the bottom of the view: pad icons and
                                  key caps, rebuilt only when the set changes
Ui/ViewportHost.cs                the 3D pane, who has the mouse, and the once-a-frame work
                                  behind it
Ui/MaterialList.cs                the materials list widget: icon, name, have / need, a bar, station
                                  rows, one footer line (the editor hides it). Two columns past 10
                                  rows when the caller allows. Pooled rows. Also on the hammer card
Ui/FocusNav.cs                    the panel walk behind F6 and L3: header, Layers, Inspector, toolbar
                                  (a folded panel is skipped), steps by screen position, the ring
Ui/Dialogs.cs                     Blueprints (open, delete your own), save as, help, the Keys window,
                                  the command search (Ctrl+K), the questions. One at a time
Ui/Toasts.cs                      short messages over the bottom middle of the view

View/EditorScene.cs               the little world: ground, grid, origin ring, front marker, two
                                  lights. 8000 m under the player, all on layer 30
View/PreviewCamera.cs             the switched-off camera that renders the pane into a texture
View/EditorCamera.cs              one camera: fly, look, pan, zoom, orbit round a point, the seven
                                  view presets, perspective or orthographic. No modes.
View/SceneModel.cs                one copy per piece, kept in step with the document, and the
                                  support tint on the piece under the aim
View/GhostRenderer.cs             the see-through copy of what is in hand, in the support colours,
                                  blinking red where a click does nothing
View/SelectionBoxes.cs            gold wire boxes round the selection, one mesh per colour
View/SnapDots.cs                  snap points: cyan nearby, yellow own, orange the pair that snapped
View/ViewportRaycast.cs           screen pixels to the pane's space and back, masked to layer 30
View/CaptureBox.cs                the capture's yellow outline on the ground, one LineRenderer
View/CaptureTint.cs               the capture's glow on world pieces: yellow taken, orange across
                                  the edge. Through MaterialMan, and every one taken off again
View/CaptureHud.cs                the capture's status line top left, under the HUD root. No
                                  controls, those are in HintRow
```

**`src/Dev/`**, Debug builds only, stripped from a Release build

```
AutoTest.cs                       the scripted session: 22 scenarios, PASS/FAIL lines, screenshots,
                                  and the art guard at the end of editor_all
AutoTestPeace.cs                  stops the AI, the spawns and the raids in the test world
AutoTestUi.cs                     scenario editor_ui: the editor screen, Quick add, themes, keymap, the Figma-like edits
```

## Where to change what

| Task | File |
|---|---|
| Colours, font, sprites | `src/Editor/Ui/UiTheme.cs` |
| Add or change a keyboard key or an action | `src/Editor/Input/Keymap.cs` (the table and presets), what it does in `Bindings.Run` |
| Add or change a pad button | `src/Editor/Input/PadBindings.cs` |
| Add or change a pad combo in the world (hammer, open the editor, capture) | `src/Editor/Input/WorldPad.cs`, read where the key is read (`PlayerUpdatePlacementPatch`, `EditorSession.Tick`, `WorldCapture.Tick`) |
| Change what F6 and L3 walk | `src/Editor/Ui/FocusNav.cs` |
| Change the controls shown under the 3D pane | `ViewportHost.ShowHints`, widgets in `src/Editor/Ui/HintBar.cs` |
| Add an editor setting | `src/Editor/EditorConfig.cs` (the config file only, there is no settings dialog) |
| Add a build setting | `src/Blueprints/BuildConfig.cs`, and put it back in `AutoTest.ResetSettings` |
| Change where a build takes materials from | `src/Blueprints/MaterialSources.cs` |
| Change which pieces a click builds, and in what order | `src/Blueprints/PartialBuild.cs` |
| Change the materials list: its numbers / its look / where it sits on the hammer card / in the editor | `src/Blueprints/MaterialTally.cs` / `src/Editor/Ui/MaterialList.cs` / `src/Blueprints/BlueprintInfoCard.cs` / `src/Editor/Ui/BlueprintPage.cs` |
| Change how an unfinished build is kept, found or shown | `src/Blueprints/Sites/` (file: `SiteStore`, world and ghosts: `SiteTracker`) |
| Change an unfinished build's ghost colours | `BlueprintPreview.ReadyTint` / `RedTint` in `src/Blueprints/BlueprintPreview.cs` |
| Change Continue: the key's order, the click, what Remove does | `src/Blueprints/BlueprintMode.cs` |
| Change the remove window: its buttons, text, keys, pad | `src/Blueprints/Sites/SiteRemovePopup.cs` (the text is in `BlueprintMode.PressRemove`) |
| Change how "Whole structure" takes pieces down, or its message | `src/Blueprints/Sites/SiteRemoval.cs` |
| Change the capture: keys, sizes, the glow, the status line | `src/Editor/WorldCapture.cs`, `View/CaptureTint.cs`, `View/CaptureHud.cs` |
| Change the hints in blueprint mode or capture (the game's row along the bottom) | `src/Blueprints/HintRow.cs` (the sets are in `Hints`) |
| Add a header button or menu item | `src/Editor/Ui/Header.cs` + the verb in `src/Editor/EditorCommands.cs` |
| Change what an edit does | `src/Editor/EditorState.cs`, the undo step in `Doc/BlueprintDocument.cs` |
| Change snapping | `src/Editor/Placement/Placement.cs` |
| Change the support rule or its colours | `src/Editor/Placement/Support.cs` |
| Change the file format | `src/Blueprints/BlueprintFormat.cs` (keep it free of other game code) |
| Add a problem to the list | `src/Editor/Checks.cs` |
| Add a game hook | a new `src/Patches/<Type><Method>Patch.cs` |
| Add a test | `src/Dev/AutoTest.cs`, then list the scenario in `scripts/autotest.sh` and `autotest.ps1` |
| Write down why something works the way it does | `.claude/design-decisions.md`, the section for that area |

---

