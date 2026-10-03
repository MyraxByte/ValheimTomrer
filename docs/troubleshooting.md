# Failure modes and their signatures

Symptom, then cause. Symptoms whose cause is a rule are named next to that rule in `.claude/rules/`.

Symptoms whose cause is not already a rule above. The rest are named next to their rule.

| Symptom | Cause |
|---|---|
| No `LogOutput.log` at all, game runs fine | arm64, `ARCHPREFERENCE` reverted (see Apple Silicon) |
| `0 plugins to load` | DLL not deployed; re-run `dotnet build` |
| Plugin loads, patch never fires | wrong method name/overload, grep log for `HarmonyException` |
| Compile error reaching a private member | assembly missing from `<Publicize>` in the csproj |
| `DllNotFoundException: AppleCoreNativeMac` | **benign**, vanilla Apple GameKit probe failing under Rosetta, unrelated to mods |
| Editor window opens but the 3D pane is black | the pane camera's culling mask lost layer 30, or its `RenderTexture` was released (`PreviewCamera.Resize`) |
| The 3D pane looks frozen with a pad in hand | focus is in a panel, press circle to go back to the pane |
| A piece shows as a plain box in the pane | the prefab has no `MeshFilter`, or it draws through `InstanceRenderer`, which a mesh-only clone cannot copy |
| Blueprint mode never starts in a test run, B does nothing | the test character's hammer broke (the character is saved on quit, run after run). `AutoTest.EquipHammer` repairs it |
| B says "No blueprints" right after a teleport | the player landed in water: swimming puts the hammer away. Pick dry land and equip again |
| Remove in Continue does nothing | the game reads Remove on release (`GetButtonUp`). On this Mac it is Left Command, not the middle mouse |
| The remove window's circle icon reads `MISSING BUTTON DEF "ButtonB"` down the screen | a copied button's hint kept the prefab's text. `SiteRemovePopup.ShowHints` sets it from `$KEY_JoyButtonB`; the game re-translates only its own texts |
| A test's mouse click on a button does nothing, the button stays pressed | `MouseState.WithButton` changes the struct it is called on, so the "release" state still had the button down. Build the press state on its own (`AutoTest.ClickScreen`) |
| The art guard fails with "the walk saw 0 unfinished-build files" | the guard's folder list (`AutoTest.WalkWrittenFiles`) lost the sites folder, or no scenario in the chain kept a site until `ClearSites` |
| The pad's L2 + triangle opens the inventory, or the D-pad moves the hotbar during a capture | `ZInputTryGetButtonStatePatch` did not apply (`ZInput.TryGetButtonState` renamed or inlined), or `WorldPad.Live` is false (a game menu is up) |
| In blueprint mode or a capture the bottom row still shows the game's snapping and copy hints | `KeyHintsUpdateHintsPatch` did not apply (`KeyHints.UpdateHints` renamed), or the log says "hint row: the game's build hints do not look as expected" (the game renamed its `Place`, `key_bkg`, `Text - Place` entries or the wheel sprite) |
| A test says the game's pad entry reads `MISSING BUTTON DEF "Place"` | normal while the keyboard is in use: the game localizes its hidden pad row with keyboard names. Compare pad icons only while `ZInput.IsGamepadActive()` |
| Save as saves its first name and closes on the pad's cross | a text box built as a plain `TMP_InputField` instead of `UiBuild.InputField` |
| A name box stops typing on the first pad or mouse press after the other device | `BuildUiOnLayoutChangedPatch` did not apply (`BuildUi.OnLayoutChanged` renamed) |
| A pad test presses a button and nothing happens | the button went to a real pad: test presses go to `AutoTest`'s own "AutoTestPad DualSense" device, never `InputSystem.FindControl`, which can pick the real DualSense |
