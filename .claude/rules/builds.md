---
paths:
  - "src/Blueprints/**"
---

# Hammer builds and unfinished builds

Read `.claude/handoff/build-sites.md` first if it exists (local file). Phase 8 (ItemDrawers) was skipped: no drawer source, no `Build/UseDrawers` setting. Reasons: `.claude/design-decisions.md` §6, §7.

- **A chest counts** (`MaterialSources.Around`) only when a player placed it, the player may open
  it, the ward lets them in, and **this client owns its ZDO** (else what was taken comes back after
  a reload). Find holds with `GetComponentInChildren<Container>`: a cart's or karve's hold has no
  `Piece`.
- **`PartialBuild.Plan` reads the world and changes nothing.** Each chosen piece is paid just before
  it goes down; one that can no longer be paid is skipped, never placed free.
- **No copy is a crafting station.** `BlueprintPreview.StripToVisuals` removes every
  `CraftingStation` from a copy (else a piece that needs a workbench builds with no real bench
  near). On a station that may be a ghost read `m_buildRange` / `m_rangeBuild`, never
  `GetStationBuildRange()`: it throws a `NullReferenceException` in `CraftingStation.GetExtensions`.
- **A site's file** is the blueprint as it was at the click, plus `#Site:` and `#SiteSource:`
  headers, read in `SiteStore` only, never in `BlueprintFormat`. What is built is never written, it is
  read from the world. Tests point `SiteStore.RootOverride` at `.devtest/sites`.
- **A site's ghost** is filled with each renderer's `forceRenderingOff` and the root unturned, then
  moved (`MeasureBounds` skips inactive renderers: filled switched off, the ghost gets a zero or wrong
  size). Its look is `SetPart`: built hidden, ready light blue (`ReadyTint`, without it a ready part
  looks like built wood), the rest red (`RedTint`), through `MaterialMan`. Only
  `PreviewStyle.Site()` is tinted, and a click never tints it.
- **In blueprint mode (Continue too) the game's `UpdatePlacement` does not run**, so Remove never
  takes down the aimed piece there.
- **The remove window** (`SiteRemovePopup`) is a `UnifiedPopup` of its own `PopupType` (100). It
  blocks through the game's `Menu.IsVisible()`, so it needs no input patch. Whenever it is not the
  popup showing, the panel's width and default button go back and its copies hide.
- **Whole structure** (`SiteRemoval.TakeDown`) gives each piece `Player.RemovePiece`'s own checks and
  calls, top to bottom (the reverse of `PartialBuild.BuildOrder`), and leaves a refused piece and
  every piece it needs to stand.
