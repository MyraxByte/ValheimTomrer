---
paths:
  - "src/**"
  - "ValheimTomrer.csproj"
---

# Hot reload safety

The mod is developed with BepInEx ScriptEngine: `npm run hot` builds into `BepInEx/scripts/` on every
save, and ScriptEngine destroys the plugin and loads a fresh copy in the running game (also on F6).
How to run it: `docs/development.md`. These rules keep a reload clean.

- **Whatever the mod creates, `ValheimTomrerPlugin.OnDestroy` takes down.** Harmony patches, GameObjects,
  materials, textures, game objects parented to the game's HUD, cached references. A reload does not
  clear them: they stack up and the second copy of every hint row or ghost shows.
- **A new thing that makes a GameObject, a game-side hook or a static cache** adds its `Destroy` /
  `Clear` call to `OnDestroy` (usually through `EditorSession.Shutdown`), in the same change.
- **No state that must survive a reload lives in a static field.** The new assembly starts with all
  statics empty. Save what matters to a file (`SiteStore`, `DocumentStore`) or read it back from the world.
- **A plugin must not be in `plugins/` and `scripts/` at once.** The csproj deploys to one folder and
  deletes the copy in the other, so never copy the DLL by hand.
- **Check a reload really happened:** the log line `ValheimTomrer 0.1.0 loaded (build <time>)` shows
  the build time of the code that is running.
- **The reload test** is part of the work when you change `OnDestroy` or add a patch: reload twice
  in a loaded world, then check the log has no `HarmonyException`, no `NullReferenceException`, and that
  F7, B and F8 still work once, not twice (one editor window, one hint row).
- A change to a patch signature, a new patch class, config entries or the csproj still needs a game
  restart if the reload does not pick it up. Say so instead of guessing.
