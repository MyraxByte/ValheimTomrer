---
paths:
  - "src/Patches/**"
---

# Patching and reading the game

Valheim has no modding API or docs. Finding a patch target means reading the game.

- **Names, signatures, enum values, private or not:** read the metadata with
  `System.Reflection.Metadata` from a throwaway `dotnet run` project in the scratchpad.
- **Method bodies and call flow:** `ilspycmd` is installed but wants .NET 8, so roll forward. One
  type at a time is quick:
  `DOTNET_ROLL_FORWARD=Major ilspycmd -t ZInput "$MANAGED/assembly_utils.dll"` (`$env:DOTNET_ROLL_FORWARD="Major"` in PowerShell; `$MANAGED` is `valheim_Data/Managed` on Windows).
  The whole assembly: `-p -o <scratchpad>/valheim-src "$MANAGED/assembly_valheim.dll"`, then grep the tree.
- **Names that only exist as text:** scan the DLL's strings. macOS `strings` has no `-el`, so UTF-16
  text needs python: `re.finditer(rb'(?:[\x20-\x7e]\x00){3,}', data)`.

**Game 1.0 facts** (values and data versions: §1):

- The build menu sorts pieces by `Piece.UsageTagFlags` (`[Flags]`, read by `ByUsagePieceList`).
  `Piece.PieceCategory` is legacy. `Piece.ComfortGroup` is unchanged.
- `Managed/` ships `Newtonsoft.Json` 13.0: do not bundle it.

**After a game update**, check that every patched method still exists (a missing one is a
`HarmonyException` in the log). By name: the nine input targets of `EditorInputBlockPatches.cs`
(`Player.TakeInput`, `PlayerController.TakeInput`, `TextInput.IsVisible`, `InventoryGui.Show`,
`HotkeyBar.Update`, `Menu.Update`, `Minimap.Update`, `GameCamera.UpdateMouseCapture`,
`ZInput.Internal_GetMouseScrollWheel`), `ZInput.TryGetButtonState` (the world pad's hold-back),
`KeyHints.UpdateHints` (the controls row) and `BuildUi.OnLayoutChanged` (a typing box keeps the keyboard).

---

## Conventions for patches

- One patch class per target, file named `<Type><Method>Patch.cs` in `src/Patches/`.
- Gate feature behaviour on `ValheimTomrerPlugin.ModEnabled.Value`.
- Do not add `[BepInProcess("valheim.exe")]`: on macOS the executable is `Valheim`, the attribute silently stops the plugin loading.
