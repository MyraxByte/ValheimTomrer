---
name: release
description: Prepare a ValheimTomrer release - bump the version everywhere, update CHANGELOG, build the Thunderstore zip. Use when asked to release, publish or package the mod.
disable-model-invocation: true
---

# Release

1. Ask for the new version number (x.y.z) if it was not given.
2. Set it in all four places, they must match (`npm run zip` fails when they differ):
   `ValheimTomrer.csproj` `<Version>`, `src/Plugin.cs` `PluginVersion`, `thunderstore/manifest.json` `version_number`, and a new top block in `CHANGELOG.md`.
3. Write the CHANGELOG block for players: short, plain words, what changed for them.
4. `npm run check`, then `npm run zip`. It builds Release (no autotest inside), checks the manifest (description 250 characters at most), the 256x256 icon, then writes `thunderstore/build/ValheimTomrer.zip`.
5. The release check `npm run autotest -- editor_all` must have passed on the code being released. If it has not, say so, do not claim it.
6. Do not upload or tag anything. Tell the owner the zip path and what is left to do by hand.
