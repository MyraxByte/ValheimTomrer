# Development guide

How to build, run, hot reload and debug ValheimTomrer on Windows and macOS. The Windows walk-through
for the project owner, in Russian, is `docs/windows-setup.ru.md`.

## Tools

| Tool | Why |
|---|---|
| Valheim 1.0 (Steam) with BepInExPack_Valheim 5.4.2350 | the game and the mod loader |
| .NET SDK 9 | builds the plugin (`netstandard2.1`) |
| Node.js 20+ | the npm scripts, the rules check, the Claude Code hook |
| BepInEx ScriptEngine (BepInEx.Debug) | hot reload, one DLL in `BepInEx/plugins` |
| VS Code (C# Dev Kit, Visual Studio Tools for Unity) or Rider | editing and the debugger |

## Where the game is

| | Windows | macOS |
|---|---|---|
| Default install | `C:\Program Files (x86)\Steam\steamapps\common\Valheim` | `~/Library/Application Support/Steam/steamapps/common/Valheim` |
| Game assemblies | `valheim_Data\Managed` | `valheim.app/Contents/Resources/Data/Managed` |
| Executable | `valheim.exe` | `valheim.app` (through `run_bepinex.sh`) |

Another folder: set the `VALHEIM_INSTALL` environment variable, or create `Directory.Build.props.user`
(git ignores it):

```xml
<Project><PropertyGroup>
  <ValheimInstall>D:\SteamLibrary\steamapps\common\Valheim</ValheimInstall>
</PropertyGroup></Project>
```

## Commands

| Command | Does |
|---|---|
| `npm run build` | Debug build, copies the DLL into `BepInEx/plugins/ValheimTomrer/` |
| `npm run build:hot` | Debug build, copies the DLL into `BepInEx/scripts/` (for ScriptEngine) |
| `npm run hot` | watches `src/` and does `build:hot` on every save |
| `npm run dev` / `dev:hot` | build, start the game, show our log lines. `-- --debug` opens the debugger |
| `npm run autotest -- editor_all` | the release check (game must be closed) |
| `npm run check` | the world-save rule and the art rule |
| `npm run zip` | the Thunderstore package |

The npm scripts call `scripts/*.ps1` on Windows and `scripts/*.sh` elsewhere (`scripts/run.mjs`).

## Hot reload

ScriptEngine loads every DLL in `BepInEx/scripts/`. When the DLL changes (or on F6) it destroys the
plugin and loads a new copy. No game restart.

1. Once: put `ScriptEngine.dll` in `BepInEx/plugins/`. Start the game once so it writes
   `BepInEx/config/com.bepis.bepinex.scriptengine.cfg`, and check `EnableFileSystemWatcher = true`
   (otherwise press F6 after every build).
2. Terminal 1: `npm run dev:hot` (builds into `scripts/`, starts the game, shows our log).
3. Load a world. Terminal 2: `npm run hot`.
4. Edit a file, save. The watcher builds, ScriptEngine reloads. The log shows
   `ValheimTomrer 0.1.0 loaded (build <time>)` with the new time.

The csproj puts the DLL in `scripts/` or in `plugins/`, never both. `npm run build` and the autotest
(which load from `plugins/`) delete the `scripts/` copy.

Needs a restart: a new or changed Harmony patch target the game already ran past, a changed
`[BepInPlugin]`, a changed csproj reference. Rules that keep a reload clean: `.claude/rules/hot-reload.md`.

## Debugging

`npm run dev -- --debug` starts the game with the Mono debugger on `127.0.0.1:10000`. In VS Code use
"Attach to Valheim" (type `vstuc`). ScriptEngine loads each reload as a renamed copy of the assembly,
so after a reload attach again, or break with `System.Diagnostics.Debugger.Break()` in the new code.
`--doorstop-mono-debug-suspend true` freezes the game at start until the debugger attaches (for
`Awake`). A `--doorstop-*` flag always takes a value.

## macOS notes

On Apple Silicon BepInEx loads nothing when the game runs natively and writes no `LogOutput.log`.
In `run_bepinex.sh` set `export ARCHPREFERENCE="x86_64,arm64"`. A BepInEx reinstall reverts it. Then
`xattr -d com.apple.quarantine libdoorstop.dylib`. Reasons: `.claude/design-decisions.md` §1.

## Logs

`BepInEx/LogOutput.log` (includes the Unity log). Vanilla Unity log: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Player.log`
on Windows, `~/Library/Logs/IronGate/Valheim/Player.log` on macOS.

## Quick checks

- Main-menu signal (no world needed, about 13 s after start): `ValheimTomrer 0.1.0 loaded (build ...)` and
  `main menu reached | harmony=OK | publicizer=OK`. It proves plugin load, Harmony and the publicizer.
- Steam does not need to run: `steam_appid.txt` in the game folder lets the game start alone.
- In game, F5 opens the console (turn it on once: Settings, Gameplay, Enable console), then `devcommands`
  unlocks `god`, `fly`, `spawn`, `tod`.
- Stop a test game: `Stop-Process -Name valheim` (Windows), `pkill -f "valheim.app/Contents/MacOS/Valheim"` (macOS).
