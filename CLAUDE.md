# ValheimTomrer

Client-side BepInEx plugin for Valheim 1.0 (Windows and macOS). Adds blueprints to the hammer, an
in-game blueprint editor (F7), world capture (F8) and unfinished builds. C#, `netstandard2.1`,
BepInEx 5 + HarmonyX, Unity 6 Mono. Game, Unity and BepInEx references come from the local install.

## Commands

| Do | Run |
|---|---|
| Build (deploys the DLL into the game) | `npm run build` (`dotnet build`) |
| Hot reload, game keeps running | `npm run dev:hot`, then `npm run hot` ([docs/development.md](docs/development.md)) |
| Build, start the game, show our log | `npm run dev` (`-- --debug` for the debugger) |
| Check the two "never break" rules | `npm run check` |
| Release check (needs the real game, about 11 min) | `npm run autotest -- editor_all` |
| Thunderstore zip | `npm run zip` |

Windows setup for the owner: `docs/windows-setup.ru.md`. Failure signatures: `docs/troubleshooting.md`.

## How to work

1. **Look before you change.** Open the file the map points to (below). Search only for a symbol
   inside a file you already know. For game code ask the `game-code-reader` agent.
2. **Verify by running, not by reading.** After a change run `npm run check` and `dotnet build`. For
   behaviour, the autotest scenario of the area, or the game itself. If the game is not on this
   machine (a cloud session), say "not run in the game" in your reply. Never write "should work".
3. **Small steps.** One feature or fix per commit. Do not widen a task. A failing check is real
   until you reproduce it alone: never skip, weaken or delete a check to get green.
4. **A new thing is not done** until: it works on a controller too, its autotest check exists, the
   README controller table has its line, the file map is updated, and (if it makes objects) the
   hot-reload cleanup is in `OnDestroy`.

## Scope: do not exceed without asking

- **Single-player, client side only.** No RPCs, no ZDO sync, no server logic.
- **No custom prefabs or pieces.** Patch existing game behaviour. No Jötunn, only BepInEx + HarmonyX.
- Old worlds and characters are out of scope, no save migration.
- **Every feature works on a controller. No exceptions.** Whatever a key, the mouse or the wheel does,
  a pad button or combo does too, in the same phase. Never mark pad support "later".

If a task crosses one of these lines, stop and ask.

## Never break

**World-save rule.** The mod writes nothing of its own into the world save: no ZDO keys, RPCs,
network messages or ServerSync. Pieces go up only through `Player.PlacePiece`, one call per piece,
and come down only through the game's own remove calls, one at a time (`SiteRemoval`). Items leave
only through the game's `Inventory` calls. An unfinished build is a `.blueprint` file under
`config/ValheimTomrer/sites/`, never in the world. If a change seems to need a ZDO key, an RPC or a
new prefab, the design is wrong: stop and ask.

**Art rule.** The mod never writes a model, mesh, texture, icon, sprite, atlas or material to disk.
Its only files are `.blueprint` text files in `config/ValheimTomrer/blueprints/` and `.../sites/`.
Everything it draws is made at runtime from what the game already loaded.

Both are checked by `npm run check` (and by a hook after every edit, and by `editor_all`). Do not weaken them.

**Reload rule.** Whatever the mod creates, `ValheimTomrerPlugin.OnDestroy` takes down. Hot reload
destroys the plugin and loads it again: what is left behind stacks up (`.claude/rules/hot-reload.md`).

## Map

All 90 source files with one line each, and a "where to change what" table: `.claude/project-map.md`.
Go there instead of searching.

| Folder | Holds |
|---|---|
| `src/Plugin.cs` | entry point, config binds, `Update` tick, `OnDestroy` cleanup |
| `src/Patches/` | one Harmony patch class per target, `<Type><Method>Patch.cs` |
| `src/Blueprints/` | file format, library, hammer builds, partial builds, unfinished builds (`Sites/`) |
| `src/Editor/` | the F7 editor: `Doc/`, `Catalog/`, `Placement/`, `Input/`, `Ui/`, `View/`, capture |
| `src/Dev/` | the autotest, Debug builds only |
| `scripts/` | dev, autotest, zip (`.sh` macOS, `.ps1` Windows), `run.mjs`, `check-rules.mjs` |
| `blueprints/`, `tests/fixtures/` | the shipped kit (embedded in the DLL), the writer's expected output |
| `docs/` | development guide, Windows setup (Russian), troubleshooting, README GIFs |

## Rules for an area

Loaded by themselves when you read files there (`.claude/rules/`):

| Rule file | Area |
|---|---|
| `editor.md` | `src/Editor/**`, the input takeover |
| `pad.md` | the controller, `src/Editor/Input/**`, `ZInput` patch |
| `capture.md` | F8 capture |
| `ui.md` | custom UI, hint row, build card |
| `builds.md` | `src/Blueprints/**`, chests, partial and unfinished builds |
| `game-code.md` | `src/Patches/**`, reading the game |
| `autotest.md` | `src/Dev/**`, scenarios |
| `hot-reload.md` | everything in `src/**` |

Reasons, measured numbers and full behaviour of each feature: `.claude/design-decisions.md` (section
numbers are quoted in the rule files). Read the section before you change how a feature behaves or
undo a decision. Skills: `/autotest`, `/release`, `/game-update`. Local files that may exist on the owner's
machine and are not in git: `.claude/handoff/`, `.claude/plans/`, `.claude/research/`.

## Conventions

- Gate feature behaviour on `ValheimTomrerPlugin.ModEnabled.Value`.
- Log through `ValheimTomrerPlugin.Log`, never `Debug.Log`.
- `OnDestroy` keeps calling `_harmony.UnpatchSelf()`.
- Keep `Blueprint.cs` and `BlueprintFormat.cs` free of other game code (the desktop editor Tomrer, a
  separate repo, compiles them alone). Do not work in `../Tomrer`.
- Do not switch BepInEx or game references to NuGet: local references guarantee the version match.
- Never copy the DLL into the game by hand: the build deploys it (to `plugins/` or, with hot reload, `scripts/`).
- Add or remove a file: update `.claude/project-map.md` in the same commit.

## Environment

| | |
|---|---|
| Game | Valheim 1.0 (`c_networkVersion = 40`), Unity `6000.0.75f1`, Mono (not IL2CPP) |
| Game code | `assembly_valheim.dll`, 1311 types, publicized by Krafs.Publicizer: private members are directly accessible |
| BepInEx | 5.4.23.5 (BepInExPack_Valheim 5.4.2350), plus ScriptEngine for hot reload |
| SDK | .NET SDK 9, Node.js 20+ |
| Install path | `VALHEIM_INSTALL`, or `Directory.Build.props.user`, else the Steam default of the OS |
| macOS only | Apple Silicon needs `ARCHPREFERENCE="x86_64,arm64"` in `run_bepinex.sh`, or no plugin loads and no log is written. A BepInEx reinstall reverts it. See `docs/development.md` |

## How to write for this user

Applies to replies, docs, plans and commit messages.

- The user is a developer and a non-native English speaker. Reply in the language they write in
  (Russian if they write Russian). Code, commits, repo docs and comments stay in English, except `docs/*.ru.md`.
- **Short.** Lead with the answer. One or two sentences is often enough.
- **Simple words.** Everyday English. No jargon, no metaphors, no em-dashes (use a comma, a period, or reword).
- **Structure over prose.** Two or more facts, options, steps or files go in a list or a table.
- **Cut** preambles, restating the task, layered qualifications, explaining your own reasoning.
- Resolve ambiguous targets yourself from the code. For open questions offer 2-3 concrete options in one pass.
- The user judges by the running result, not by diffs.

## Maintaining this file

It loads on every turn, so each line costs on every turn. Keep it under 150 lines. Before adding a line:

1. **What, not why.** A rule is one line. Reasons, history and numbers go to `.claude/design-decisions.md`.
2. **Area rules go to `.claude/rules/<area>.md`** with `paths:`, not here.
3. **Already checked?** If the compiler, `npm run check` or the autotest fails on the mistake, do not write it down.
4. **Already said?** Search first. Remove a stale line as readily as you add a true one.
5. A repeated multi-step workflow becomes a skill in `.claude/skills/`, not text here.
6. A rule that must hold every time becomes a hook in `.claude/settings.json`, not a sentence.
