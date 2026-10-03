---
name: game-update
description: Check that every Harmony patch of ValheimTomrer still matches the game after a Valheim update. Use when the game version changed or a HarmonyException shows in the log.
---

# After a game update

1. Read the game version from `BepInEx/LogOutput.log` or the game folder, and compare it with the one in `CLAUDE.md`. Update the table there.
2. Build: `dotnet build`. A missing or renamed member is a compile error (the game's assemblies are publicized).
3. Start the game once (`npm run dev`) and search the log for `HarmonyException`. A missing patch target shows there.
4. Check these targets by name (use the `game-code-reader` agent, one type at a time):
   - the nine input targets of `src/Patches/EditorInputBlockPatches.cs`: `Player.TakeInput`, `PlayerController.TakeInput`, `TextInput.IsVisible`, `InventoryGui.Show`, `HotkeyBar.Update`, `Menu.Update`, `Minimap.Update`, `GameCamera.UpdateMouseCapture`, `ZInput.Internal_GetMouseScrollWheel`
   - `ZInput.TryGetButtonState` (the world pad's hold-back)
   - `KeyHints.UpdateHints` (the controls row), `BuildUi.OnLayoutChanged` (a typing box keeps the keyboard)
   - the rest: `ls src/Patches`, one file per target, named `<Type><Method>Patch.cs`
5. Run the release check (`/autotest`, `editor_all`) and fix what fails.
6. Record new data versions or renamed things in `.claude/design-decisions.md` §1.
