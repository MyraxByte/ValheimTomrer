#if DEBUG
using System.Collections;
using System.Linq;
using ValheimTomrer.Editor;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Input;
using ValheimTomrer.Editor.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimTomrer.Dev
{
    /// <summary>
    /// Scenario "editor_ui": the new screen (the header islands and the menu, the floating Layers and
    /// Inspector cards, the toolbar, the full-screen view under them), the 3D tools (views, orbit,
    /// orthographic, gizmo, isolate), Quick add, the wheel, the themes, the keymap, the Keys window
    /// and the command search, hide and lock, the Figma-like edits (align, spread, mirror, copy in a
    /// row, Shift + arrow) and the snapping settings. Keyboard and pad where both exist.
    /// </summary>
    internal static partial class AutoTest
    {
        private static IEnumerator TestEditorUi(Player player)
        {
            yield return new WaitForSeconds(0.5f);
            yield return PressKey(UnityEngine.InputSystem.Key.F7);
            yield return WaitPaneReady();
            var document = EditorSession.Document;
            Check(ModUi.Open && document != null && document.Pieces.Count >= 3,
                $"the key opened the editor on a kit: '{(document != null ? document.Name : "nothing")}'");
            if (!ModUi.Open || document == null || document.Pieces.Count < 3)
            {
                yield break;
            }

            yield return RedesignLayout();
            yield return RedesignIslands();
            yield return RedesignSpace(document);
            yield return RedesignQuickAdd();
            yield return RedesignWheel();
            yield return RedesignTheme(document);
            RedesignKeymap();
            yield return RedesignWindows();
            RedesignHideLock(document);
            RedesignShapes(document);
            RedesignSnapping(document);
            EditorSession.Close();
        }

        /// <summary>
        /// The window is the screen and the view is all of it, whatever the islands do (Alt+1, Alt+2 put
        /// the cards away); Ctrl+\ and L2 + L3 hide everything but the view; the header's menu opens
        /// and Esc closes it; the Inspector's tabs switch; the toolbar shows the settings.
        /// </summary>
        private static IEnumerator RedesignLayout()
        {
            EditorWindow.SetLayers(true);
            EditorWindow.SetInspector(true);
            yield return Frames(2);
            var root = EditorWindow.Root.rect;
            var view = EditorWindow.ViewportHost.rect;
            Check(Mathf.Abs(root.width - Screen.width / EditorWindow.Root.lossyScale.x) < 2f,
                $"the window is exactly the screen: {root.width:0} units, screen {Screen.width} px");
            Check(Mathf.Abs(view.width - root.width) < 1f && Mathf.Abs(view.height - root.height) < 1f,
                $"the view is the whole screen, islands or not: {view.width:0}x{view.height:0}");

            Bindings.Press(KeyCode.Alpha1, KeyMods.Alt);
            Bindings.Press(KeyCode.Alpha2, KeyMods.Alt);
            yield return null;
            Check(!EditorWindow.LayersOpen && !EditorWindow.InspectorOpen
                && EditorWindow.LeftDock.anchoredPosition.x < -1000f && EditorWindow.RightDock.anchoredPosition.x > 1000f
                && Mathf.Abs(EditorWindow.ViewportHost.rect.width - root.width) < 1f
                && Mathf.Abs(EditorWindow.ViewportHost.rect.height - root.height) < 1f,
                $"Alt+1 and Alt+2 put both cards away and the view stays the whole screen ({EditorWindow.ViewportHost.rect.width:0}x{EditorWindow.ViewportHost.rect.height:0})");
            Check(EditorWindow.LayersTab.gameObject.activeSelf && EditorWindow.InspectorTab.gameObject.activeSelf,
                "a small tab on each edge stands where a card was");
            EditorWindow.LayersTab.GetComponentInChildren<Button>().onClick.Invoke();
            EditorWindow.InspectorTab.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            Check(EditorWindow.LayersOpen && EditorWindow.InspectorOpen && !EditorWindow.LayersTab.gameObject.activeSelf,
                "a click on a tab brings its card back");
            LayersPanel.HideButton.onClick.Invoke();
            Inspector.HideButton.onClick.Invoke();
            yield return null;
            Check(!EditorWindow.LayersOpen && !EditorWindow.InspectorOpen, "the Hide button of each card puts it away");
            Bindings.Press(KeyCode.Alpha1, KeyMods.Alt);
            Bindings.Press(KeyCode.Alpha2, KeyMods.Alt);
            yield return null;
            Check(EditorWindow.LayersOpen && EditorWindow.InspectorOpen, "and back");

            EditorState.Select(new int[0]);
            Bindings.Press(KeyCode.Backslash, KeyMods.Ctrl);
            yield return Frames(2);
            Check(EditorWindow.UiHidden && EditorWindow.Header.anchoredPosition.y > 1000f && !EditorWindow.LeftShown
                && !EditorWindow.RightShown && ViewGizmo.Root.anchoredPosition.y > 1000f
                && Mathf.Abs(EditorWindow.ViewportHost.rect.height - root.height) < 1f,
                "Ctrl+\\ hides the header, both cards, the toolbar and the gizmo, and the view stays the whole screen");
            Check(Bindings.Cancel() && !EditorWindow.UiHidden && ModUi.Open, "Esc brings the interface back and keeps the window open");

            _pad = new PadState();
            PadReader.Fake = _pad;
            yield return Frames(3);
            EditorState.CancelMode();
            yield return Tap(PadButton.L2, PadButton.L3);
            Check(EditorWindow.UiHidden && !FocusNav.Active, "L2 + L3 on the pad hides the interface, no panel walk");
            yield return Tap(PadButton.L2, PadButton.L3);
            Check(!EditorWindow.UiHidden, "L2 + L3 again brings it back");

            // The pad walk reaches the header, both panels and the toolbar.
            yield return Tap(PadButton.L3);
            var regions = new System.Collections.Generic.HashSet<FocusRegion> { FocusNav.Current };
            for (var i = 0; i < 4; i++)
            {
                yield return Tap(PadButton.R1);
                regions.Add(FocusNav.Current);
            }

            Check(regions.Contains(FocusRegion.TopBar) && regions.Contains(FocusRegion.Left) && regions.Contains(FocusRegion.Right)
                && regions.Contains(FocusRegion.Bottom),
                $"L3 and R1 walk the header, Layers, the Inspector and the toolbar: {string.Join(", ", regions)}");
            yield return Tap(PadButton.Circle);
            PadReader.Fake = null;
            _pad = null;
            yield return Frames(2);

            Header.ToggleMenu();
            yield return null;
            Check(Header.MenuOpen, "the menu button opens the menu");
            Check(Bindings.Cancel() && !Header.MenuOpen && ModUi.Open, "Esc closes the menu first");

            Inspector.SetTab(Inspector.BlueprintTab);
            yield return null;
            Check(Inspector.Tab == Inspector.BlueprintTab && BlueprintPage.NameField != null && BlueprintPage.ChoiceCount > 1,
                $"the Blueprint tab has the name box and {BlueprintPage.ChoiceCount} icon choices");
            Inspector.SetTab(Inspector.ChecksTab);
            yield return null;
            Check(Inspector.Tab == Inspector.ChecksTab, $"the Checks tab: '{ChecksPage.SummaryText}'");
            Inspector.SetTab(Inspector.DesignTab);
            Check(LayersPanel.GroupCount > 0 && LayersPanel.RowCount > 0,
                $"Layers groups the pieces: {LayersPanel.GroupCount} groups, {LayersPanel.RowCount} rows");
            Check(Toolbar.GridText.StartsWith("Grid"), $"the toolbar shows the grid: '{Toolbar.GridText}'");
            yield return Screenshot("editor-ui-1-window");
        }

        /// <summary>
        /// The islands: opaque, inside the screen, none over another, every word fits its button, the
        /// lines over the view stay inside the room the islands leave, and the toolbar was not under the picture.
        /// </summary>
        private static IEnumerator RedesignIslands()
        {
            EditorWindow.SetLayers(true);
            EditorWindow.SetInspector(true);
            yield return Frames(3);

            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            var islands = EditorWindow.Header.GetComponentsInChildren<RectTransform>(false)
                .Where(r => r.name == "File" || r.name == "Modes" || r.name == "Actions")
                .Concat(new[] { EditorWindow.LeftDock, EditorWindow.RightDock })
                .ToList();
            Check(islands.Count == 5, $"the header has three islands and there are two cards: {islands.Count} found");

            var outside = islands.Where(r => !Contains(screen, ScreenBox(r))).Select(r => r.name).ToList();
            Check(outside.Count == 0, $"every island is inside the screen: {(outside.Count == 0 ? "yes" : string.Join(", ", outside))}");

            var overlaps = new System.Collections.Generic.List<string>();
            for (var a = 0; a < islands.Count; a++)
            {
                for (var b = a + 1; b < islands.Count; b++)
                {
                    if (ScreenBox(islands[a]).Overlaps(ScreenBox(islands[b])))
                    {
                        overlaps.Add(islands[a].name + "/" + islands[b].name);
                    }
                }
            }

            Check(overlaps.Count == 0, $"no island is over another: {(overlaps.Count == 0 ? "none" : string.Join(", ", overlaps))}");

            var clear = islands.Select(r => r.GetComponent<Image>()).Where(i => i != null && i.color.a < 0.999f).Select(i => i.name).ToList();
            Check(clear.Count == 0, $"the islands are opaque: {(clear.Count == 0 ? "all" : string.Join(", ", clear))}");

            var toolbar = ScreenBox(Toolbar.Root);
            Check(!islands.Any(r => ScreenBox(r).Overlaps(toolbar)) && Contains(screen, toolbar),
                "the toolbar is on the screen and clear of every island");
            Check(Toolbar.Root.GetComponentInParent<Canvas>() != null && Toolbar.Root.parent.parent == EditorWindow.Root
                && Toolbar.Root.parent.GetSiblingIndex() > EditorWindow.ViewportHost.GetSiblingIndex(),
                "the toolbar is drawn over the picture, not under it");

            var cut = new System.Collections.Generic.List<string>();
            foreach (var host in new[] { EditorWindow.Header, Toolbar.Root })
            {
                foreach (var label in host.GetComponentsInChildren<TMPro.TextMeshProUGUI>(false))
                {
                    if (label.preferredWidth > label.rectTransform.rect.width + 1.5f)
                    {
                        cut.Add($"'{label.text}' {label.preferredWidth:0}>{label.rectTransform.rect.width:0}");
                    }
                }
            }

            Check(cut.Count == 0, $"every word in the header and the toolbar fits: {(cut.Count == 0 ? "yes" : string.Join("; ", cut))}");

            var view = ScreenBox(EditorWindow.ViewportHost);
            Check(Mathf.Abs(view.width - Screen.width) < 2f && Mathf.Abs(view.height - Screen.height) < 2f,
                "the view box is the screen box");

            // What is drawn over the view stays in the room between the cards.
            var status = ScreenBox(EditorWindow.StatusText.rectTransform);
            Check(status.xMax <= ScreenBox(EditorWindow.RightDock).xMin + 1f || status.yMax < ScreenBox(EditorWindow.RightDock).yMin,
                "the status line does not run under the Inspector");

            var again = ScreenBox(EditorWindow.LeftDock);
            EditorWindow.ApplyLayout();
            EditorWindow.ApplyLayout();
            Check(ScreenBox(EditorWindow.LeftDock) == again, "laying out twice moves nothing");
            yield return Screenshot("editor-ui-islands");
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 1f && inner.yMin >= outer.yMin - 1f
                && inner.xMax <= outer.xMax + 1f && inner.yMax <= outer.yMax + 1f;
        }

        /// <summary>
        /// Working in the 3D space: the number keys and the gizmo snap to a side, 5 switches to the flat
        /// view, an orbit keeps its point where it is, the selection's size shows, Isolate hides the rest.
        /// The same on the pad: L2 + D-pad, L1 + the right stick.
        /// </summary>
        private static IEnumerator RedesignSpace(BlueprintDocument document)
        {
            var camera = ValheimTomrer.Editor.Ui.ViewportHost.Camera;
            Check(camera != null, "the view has a camera");
            if (camera == null)
            {
                yield break;
            }

            EditorState.Select(new[] { document.Pieces[0].Id });
            yield return Frames(2);
            var size = EditorWindow.StatusText.text;
            Check(size.Contains("selected,") && size.Contains("×") && size.EndsWith(" m"),
                $"the status shows how big the selection is: '{size}'");

            Bindings.Press(KeyCode.Alpha3, KeyMods.None);
            yield return Frames(2);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Top && camera.Forward.y < -0.999f,
                $"3 looks straight down: {camera.CurrentView}, forward y {camera.Forward.y:0.000}");
            Bindings.Press(KeyCode.Alpha1, KeyMods.None);
            yield return Frames(2);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Front && camera.Forward.z < -0.999f,
                $"1 looks from the front, along -Z: forward z {camera.Forward.z:0.000}");
            Check(ViewGizmo.LabelText == "Front", $"the gizmo names the side: '{ViewGizmo.LabelText}'");
            Bindings.Press(KeyCode.Alpha2, KeyMods.None);
            yield return Frames(2);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Right && camera.Forward.x < -0.999f,
                $"2 looks from the right, along -X: forward x {camera.Forward.x:0.000}");
            Bindings.Press(KeyCode.Alpha1, KeyMods.Ctrl);
            yield return Frames(2);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Back && camera.Forward.z > 0.999f, "Ctrl+1 looks from the back");

            ViewGizmo.Click(4);
            yield return Frames(2);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Front, "a click on the gizmo's Z disc looks from the front");
            Check(ViewGizmo.DiscCount == 6, "the gizmo has six discs");

            var before = camera.Pivot;
            Bindings.Press(KeyCode.Alpha5, KeyMods.None);
            yield return Frames(2);
            var preview = ValheimTomrer.Editor.Ui.ViewportHost.Preview.Unity;
            Check(camera.Orthographic && preview.orthographic && ViewGizmo.ModeText == "Orthographic",
                $"5 switches to the flat view: ortho {preview.orthographic}, button '{ViewGizmo.ModeText}'");
            Check((camera.Pivot - before).magnitude < 0.01f, "and the camera keeps looking at the same point");
            var seen = preview.orthographicSize;
            camera.Zoom(-300f, new Vector2(0.5f, 0.5f));
            Check(preview.orthographicSize < seen && (camera.Pivot - before).magnitude < 0.01f,
                $"the wheel zooms the flat view by its size: {seen:0.00} to {preview.orthographicSize:0.00}");
            Bindings.Press(KeyCode.Alpha5, KeyMods.None);
            yield return Frames(2);
            Check(!camera.Orthographic && !preview.orthographic, "5 again is perspective");

            // An orbit turns round a point and leaves it where it shows.
            var point = ValheimTomrer.Editor.Ui.ViewportHost.OrbitPoint();
            var distance = (camera.Position - point).magnitude;
            var yaw = camera.Yaw;
            camera.Orbit(point, 35f, 10f);
            Check(Mathf.Abs((camera.Position - point).magnitude - distance) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(camera.Yaw, yaw) + 35f) < 0.01f,
                $"an orbit keeps the distance to its point ({distance:0.00} m) and turns by 35 degrees");
            camera.OrbitDrag(point, new Vector2(40f, 0f), 800f);
            Check(Mathf.Abs((camera.Position - point).magnitude - distance) < 0.01f, "a drag with Alt held orbits the same way");

            // Isolate: only the selection stays.
            var count = document.Pieces.Count;
            EditorState.Select(new[] { document.Pieces[0].Id });
            Bindings.Press(KeyCode.I, KeyMods.None);
            yield return Frames(2);
            Check(EditorState.HiddenCount == count - 1 && EditorState.SelectionCount == 1,
                $"I shows only the selection: {EditorState.HiddenCount} hidden of {count}");
            Bindings.Press(KeyCode.I, KeyMods.None);
            yield return Frames(2);
            Check(EditorState.HiddenCount == 0, "I again brings everything back");

            // The pad: L2 + D-pad cycles the sides, up is the flat view, L1 + the right stick orbits.
            _pad = new PadState();
            PadReader.Fake = _pad;
            yield return Frames(3);
            EditorState.CancelMode();
            var canUndo = document.CanUndo;
            var canRedo = document.CanRedo;
            ValheimTomrer.Editor.Ui.ViewportHost.ShowView(ValheimTomrer.Editor.View.ViewPreset.Front);
            yield return Tap(PadButton.L2, PadButton.Right);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Right, $"L2 + D-pad right: the next side, {camera.CurrentView}");
            yield return Tap(PadButton.L2, PadButton.Left);
            Check(camera.CurrentView == ValheimTomrer.Editor.View.ViewPreset.Front, $"L2 + D-pad left: back, {camera.CurrentView}");
            yield return Tap(PadButton.L2, PadButton.Up);
            Check(camera.Orthographic, "L2 + D-pad up: the flat view");
            yield return Tap(PadButton.L2, PadButton.Up);
            Check(!camera.Orthographic, "and back to perspective");
            Check(document.CanUndo == canUndo && document.CanRedo == canRedo, "L2 + D-pad is not undo or redo");

            var pad = camera.Yaw;
            var aim = ValheimTomrer.Editor.Ui.ViewportHost.OrbitPoint();
            var reach = (camera.Position - aim).magnitude;
            _pad.Down.Add(PadButton.L1);
            _pad.Rs = new Vector2(1f, 0f);
            yield return Wait(0.4f);
            _pad.Rs = Vector2.zero;
            _pad.Down.Remove(PadButton.L1);
            yield return Frames(2);
            Check(Mathf.Abs(Mathf.DeltaAngle(camera.Yaw, pad)) > 5f && Mathf.Abs((camera.Position - aim).magnitude - reach) < 0.05f,
                $"L1 + the right stick orbits round the selection: turned {Mathf.DeltaAngle(camera.Yaw, pad):0} degrees, distance kept");
            PadReader.Fake = null;
            _pad = null;
            yield return Frames(2);
            yield return Screenshot("editor-ui-space");
        }

        /// <summary>Tab opens Quick add with the search box typing; a pick goes in hand, closes it and heads Recent; stars filter.</summary>
        private static IEnumerator RedesignQuickAdd()
        {
            Bindings.Press(KeyCode.Tab, KeyMods.None);
            yield return Frames(2);
            Check(QuickAdd.IsOpen && ModUi.Typing, $"Tab opens Quick add with the keyboard in its search box (typing: {ModUi.Typing})");

            yield return PressKey(UnityEngine.InputSystem.Key.Tab);
            yield return Frames(2);
            Check(!QuickAdd.IsOpen && !ModUi.Typing, "Tab closes it again from inside the search box");

            var wall = PieceCatalog.Find("woodwall");
            Check(wall != null, "the catalog has the wood wall");
            if (wall == null)
            {
                yield break;
            }

            QuickAdd.Open();
            yield return Frames(2);
            QuickAdd.SetSearch("woodwall");
            yield return Frames(2);
            Check(QuickAdd.ShownCount >= 1 && QuickAdd.Lit == wall,
                $"the search finds the wall and lights it first: {QuickAdd.ShownCount} shown, lit '{(QuickAdd.Lit != null ? QuickAdd.Lit.PrefabName : "none")}'");
            yield return Screenshot("editor-ui-2-quick-add");
            QuickAdd.Pick();
            yield return null;
            Check(!QuickAdd.IsOpen && EditorState.Mode == EditMode.Place && EditorState.Held == wall,
                "a piece picked in Quick add goes in hand and the popup closes");
            Check(PieceMemory.Recent.Count > 0 && PieceMemory.Recent[0] == "woodwall", "and it heads the Recent list");
            EditorState.CancelMode();

            QuickAdd.Open();
            yield return Frames(2);
            QuickAdd.SetSearch("");
            QuickAdd.ToggleStar(wall);
            QuickAdd.SetTag(QuickAdd.FavouriteKey);
            Check(PieceMemory.IsFavourite("woodwall") && QuickAdd.ShownCount == 1,
                $"a starred piece is the only one under Starred: {QuickAdd.ShownCount} shown");
            QuickAdd.SetTag(QuickAdd.RecentKey);
            Check(QuickAdd.ShownCount >= 1 && QuickAdd.Lit == wall, $"Recent lists what was used, newest first: {QuickAdd.ShownCount} shown");
            QuickAdd.ToggleStar(wall);
            QuickAdd.SetTag(null);
            Check(!PieceMemory.IsFavourite("woodwall"), "a second right click takes the star off");
            Check(Bindings.Cancel() && !QuickAdd.IsOpen, "Esc closes Quick add");
        }

        /// <summary>One wheel notch scrolls the palette grid by its step, whatever units the platform sends.</summary>
        private static IEnumerator RedesignWheel()
        {
            QuickAdd.Open();
            QuickAdd.SetSearch("");
            yield return Frames(3);
            var grid = EditorWindow.Root.GetComponentsInChildren<ScrollRect>(false).FirstOrDefault(s => s.name == "Grid");
            var wheel = grid != null ? grid.GetComponent<WheelScroll>() : null;
            Check(wheel != null, "the Quick add grid scrolls with WheelScroll");
            if (wheel == null || EventSystem.current == null)
            {
                QuickAdd.Close();
                yield break;
            }

            var room = grid.content.rect.height - grid.viewport.rect.height;
            var start = grid.content.anchoredPosition.y;
            wheel.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
            yield return Wait(0.6f);
            var one = grid.content.anchoredPosition.y;
            var expected = Mathf.Min(Mathf.Max(wheel.Step, grid.viewport.rect.height * 0.22f), room - start);
            Check(Mathf.Abs((one - start) - expected) < 1f,
                $"one notch down scrolls {one - start:0} units (step {wheel.Step:0}, room {room:0})");

            wheel.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, 120f) });
            yield return Wait(0.6f);
            Check(Mathf.Abs(grid.content.anchoredPosition.y - start) < 1f,
                $"a raw Windows notch (120) up scrolls one step back: {grid.content.anchoredPosition.y:0} (start {start:0})");
            QuickAdd.Close();
        }

        /// <summary>The theme switch rebuilds the window in the other colours and keeps the blueprint.</summary>
        private static IEnumerator RedesignTheme(BlueprintDocument document)
        {
            var dark = UiTheme.Dark;
            var generation = UiTheme.Generation;
            EditorCommands.ToggleTheme();
            yield return Frames(3);
            Check(UiTheme.Dark != dark && UiTheme.Generation > generation && ModUi.Open && EditorSession.Document == document,
                $"the theme switch rebuilt the window (dark {dark} -> {UiTheme.Dark}) and kept the blueprint");
            ViewportHost.Preview?.Render();
            var camera = ViewportHost.Preview != null ? ViewportHost.Preview.Unity : null;
            Check(camera != null && camera.backgroundColor == UiTheme.SceneBackground, "the 3D view took the theme's background");
            EditorCommands.ToggleTheme();
            yield return Frames(3);
            Check(UiTheme.Dark == dark && ModUi.Open, "and back");
        }

        /// <summary>The table picks the most specific chord, the presets differ, rebinding moves a taken key, reset puts it back.</summary>
        private static void RedesignKeymap()
        {
            Check(Keymap.Match(KeyCode.Z, KeyMods.Ctrl) == Act.Undo
                && Keymap.Match(KeyCode.Z, KeyMods.Ctrl | KeyMods.Shift) == Act.Redo
                && Keymap.Match(KeyCode.G, KeyMods.Shift) == Act.Move
                && Keymap.Match(KeyCode.R, KeyMods.Shift) == Act.RotateLeft
                && Keymap.Match(KeyCode.R, KeyMods.None) == Act.RotateRight
                && Keymap.Match(KeyCode.Z, KeyMods.None) == Act.None
                && Keymap.Match(KeyCode.Z, KeyMods.Cmd) == Act.Undo,
                "the Tomrer keys: Ctrl+Z, Ctrl+Shift+Z, Shift held with G and R, Cmd as Ctrl");

            Keymap.SetPreset("Figma");
            var figma = Keymap.Match(KeyCode.V, KeyMods.None) == Act.Move && Keymap.Match(KeyCode.Slash, KeyMods.Ctrl) == Act.Commands;
            Keymap.SetPreset("Blender");
            var blender = Keymap.Match(KeyCode.X, KeyMods.None) == Act.Delete && Keymap.Match(KeyCode.A, KeyMods.Shift) == Act.QuickAdd
                && Keymap.Match(KeyCode.A, KeyMods.None) == Act.None;
            Keymap.SetPreset("Tomrer");
            Check(figma && blender, $"the Figma preset moves with V, Blender deletes with X and adds with Shift+A (figma {figma}, blender {blender})");

            Keymap.StartCapture(Act.Frame);
            Keymap.Capture(KeyCode.J, KeyMods.None);
            Check(Keymap.Describe(Act.Frame) == "J" && Keymap.Match(KeyCode.J, KeyMods.None) == Act.Frame
                && Keymap.Match(KeyCode.F, KeyMods.None) == Act.None,
                $"a new key for Look at the selection: {Keymap.Describe(Act.Frame)}");
            Keymap.StartCapture(Act.Frame);
            var taken = Keymap.Capture(KeyCode.G, KeyMods.None);
            Check(taken == Keymap.Of(Act.Move).Label && Keymap.Match(KeyCode.G, KeyMods.None) == Act.Frame
                && Keymap.Chords(Act.Move).Length == 0,
                $"a key another action had moves over: taken from '{taken}', Move now '{Keymap.Describe(Act.Move)}'");
            Keymap.Reset(Act.Move);
            Check(Keymap.Describe(Act.Move) == "G", "a right click gives one action its preset key back");
            Keymap.ResetAll();
            Check(Keymap.Match(KeyCode.G, KeyMods.None) == Act.Move && Keymap.Match(KeyCode.F, KeyMods.None) == Act.Frame
                && !Keymap.IsChanged(Act.Frame), "Reset all puts every key back");
            Check(Bindings.Keys.Length > Keymap.All.Count / 2, $"the help reads its keys from the table: {Bindings.Keys.Length} rows");
        }

        /// <summary>The Keys window and the command search open, list, run and close; the pad walks them.</summary>
        private static IEnumerator RedesignWindows()
        {
            Bindings.Press(KeyCode.H, KeyMods.None);
            yield return null;
            Check(Dialogs.Kind == "help", "H opens the help");
            Dialogs.Submit();
            yield return null;
            Check(Dialogs.Kind == "keys", "its Change keys button opens the Keys window");
            Dialogs.Close();

            Bindings.Press(KeyCode.K, KeyMods.Ctrl);
            yield return Frames(2);
            Check(Dialogs.Kind == "commands" && ModUi.Typing, "Ctrl+K opens the command search, typing");
            FocusNav.StopTyping();
            Dialogs.Close();

            _pad = new PadState();
            PadReader.Fake = _pad;
            yield return Frames(3);
            yield return Tap(PadButton.R3);   // the pad is in use now, so the search opens on the walk
            Dialogs.Commands();
            yield return Frames(2);
            Check(FocusNav.InDialog && !ModUi.Typing, "opened from the pad the search starts in the walk, not typing");
            yield return Tap(PadButton.Circle);
            yield return Frames(2);
            Check(!Dialogs.IsOpen && ModUi.Open, "circle closes it and the window stays");
            PadReader.Fake = null;
            _pad = null;
            yield return Frames(2);
        }

        /// <summary>Hide and lock keep pieces out of the selection; Show all brings them back; select the same kind.</summary>
        private static void RedesignHideLock(BlueprintDocument document)
        {
            var ids = document.Pieces.Select(p => p.Id).ToList();
            EditorState.Select(ids[0]);
            EditorState.HideSelection();
            EditorState.SelectAll();
            Check(EditorState.IsHidden(ids[0]) && !EditorState.IsSelected(ids[0]) && EditorState.SelectionCount == ids.Count - 1,
                "a hidden piece is left out of Select all");
            EditorState.Select(ids[1]);
            EditorState.LockSelection();
            EditorState.Select(ids[1]);
            Check(EditorState.IsLocked(ids[1]) && EditorState.SelectionCount == 0, "a locked piece cannot be selected");
            Bindings.Press(KeyCode.H, KeyMods.Alt | KeyMods.Shift);
            Check(EditorState.HiddenCount == 0 && EditorState.LockedCount == 0, "Alt+Shift+H shows and unlocks everything");

            EditorState.Select(ids[0]);
            EditorState.SelectSimilar();
            var kind = document.Pieces[0].PrefabName;
            var same = document.Pieces.Count(p => p.PrefabName == kind);
            Check(EditorState.SelectionCount == same, $"Same kind selects all {same} pieces of the first one's kind");
            EditorState.Select(new int[0]);
        }

        /// <summary>The grid rounds a free spot, the arrows move a grid step, the turn step changes R, snap points switch off.</summary>
        private static void RedesignSnapping(BlueprintDocument document)
        {
            EditorCommands.CycleGrid();
            Check(Mathf.Approximately(EditorState.GridStep, 0.25f), $"Alt+G's first step is a 0.25 m grid: {EditorState.GridStep}");
            EditorCommands.CycleGrid();
            Check(Mathf.Approximately(EditorState.GridStep, 0.5f), "and the next 0.5 m");

            var wall = PieceCatalog.Find("woodwall");
            if (wall != null && EditorSession.Document != null)
            {
                EditorSession.StartAdd(wall);
                var aimed = EditorState.Aim(new Vector3(103.13f, 20f, 97.41f), Vector3.down, out var result);
                Check(aimed && result.GridSnapped
                    && Mathf.Abs(result.Pos.x / 0.5f - Mathf.Round(result.Pos.x / 0.5f)) < 1e-3f
                    && Mathf.Abs(result.Pos.z / 0.5f - Mathf.Round(result.Pos.z / 0.5f)) < 1e-3f,
                    $"a wall aimed at bare ground lands on the 0.5 m grid: {(result != null ? result.Pos.ToString("F3") : "no hit")}");
                EditorState.CancelMode();
            }

            var id = document.Pieces[0].Id;
            EditorState.Select(id);
            var at = document.Find(id).Position;
            Bindings.Press(KeyCode.UpArrow, KeyMods.None);
            var moved = document.Find(id).Position - at;
            Check(Mathf.Abs(new Vector2(moved.x, moved.z).magnitude - 0.5f) < 1e-3f, $"with a grid the arrows move one grid step: {moved.magnitude:0.###} m");
            EditorState.Undo();

            EditorCommands.CycleAngle();
            Check(Mathf.Approximately(EditorState.AngleStep, 45f), $"Alt+R's next turn step is 45 degrees: {EditorState.AngleStep}");
            var yaw = DesignPage.YawOf(document.Find(id).Rotation);
            Bindings.Press(KeyCode.R, KeyMods.None);
            var turned = DesignPage.YawOf(document.Find(id).Rotation);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw + 45f, turned)) < 0.01f, $"and R turns 45 degrees: {yaw:0.#} -> {turned:0.#}");
            EditorState.Undo();

            EditorCommands.ToggleSnapPoints();
            Check(!EditorConfig.SnapPoints.Value, "Alt+S switches the snap points off");
            EditorCommands.ToggleSnapPoints();
            EditorConfig.GridStep.Value = 0f;
            EditorConfig.AngleStep.Value = 22.5f;
            EditorState.Select(new int[0]);
        }

        /// <summary>Align, spread, mirror, copy in a row and Shift + arrow, each one undo step.</summary>
        private static void RedesignShapes(BlueprintDocument document)
        {
            var three = document.Pieces.Take(3).Select(p => p.Id).ToList();
            while (EditorState.AlignAxis != 0)
            {
                EditorState.CycleAlignAxis();
            }

            EditorState.Select(three);
            var undoBefore = document.UndoDepth;
            EditorState.AlignSelection(-1);
            var lows = EditorState.SelectedPieces().Select(p => EditorState.BoxOf(p).min.x).ToList();
            Check(lows.Max() - lows.Min() < 1e-3f, $"Align low lines the boxes' low edges up along X: spread {lows.Max() - lows.Min():0.####}");
            if (document.UndoDepth > undoBefore)
            {
                EditorState.Undo();
            }

            EditorState.Select(three);
            EditorState.SpreadSelection();
            var boxes = EditorState.SelectedPieces().Select(p => EditorState.BoxOf(p)).OrderBy(b => b.center.x).ToList();
            var gapA = boxes[1].min.x - boxes[0].max.x;
            var gapB = boxes[2].min.x - boxes[1].max.x;
            Check(Mathf.Abs(gapA - gapB) < 1e-3f, $"Spread makes the gaps equal: {gapA:0.###} and {gapB:0.###}");
            EditorState.Undo();

            EditorState.Select(three);
            var before = EditorState.SelectedPieces().Select(p => p.Position).ToList();
            EditorState.MirrorSelection(0);
            var mirrored = EditorState.SelectedPieces().Select(p => p.Position).ToList();
            EditorState.Undo();
            var back = EditorState.SelectedPieces().Select(p => p.Position).ToList();
            var middle = (EditorState.BoxOf(EditorState.SelectedPieces()) ?? new Bounds()).center.x;
            Check(Mathf.Abs((mirrored[0].x + before[0].x) * 0.5f - middle) < 1e-3f && (back[0] - before[0]).sqrMagnitude < 1e-6f,
                "Mirror X puts a piece on the other side of the middle, and undo puts it back");

            var count = document.Pieces.Count;
            EditorState.Select(three);
            EditorState.CopyInRow(Vector3.right);
            Check(document.Pieces.Count == count + 3 && EditorState.SelectionCount == 3, "Copy in a row adds a copy of each and selects the copies");
            EditorState.Undo();
            Check(document.Pieces.Count == count, "and one undo takes them away");

            EditorState.Select(three[0]);
            var at = document.Find(three[0]).Position;
            Bindings.Press(KeyCode.UpArrow, KeyMods.Shift);
            Bindings.SetMods(KeyMods.None);
            var moved = document.Find(three[0]).Position - at;
            Check(Mathf.Abs(new Vector2(moved.x, moved.z).magnitude - (4f * Bindings.NudgeStep)) < 1e-3f,
                $"Shift + arrow nudges 4 steps: {moved.magnitude:0.###} m");
            EditorState.Undo();
            EditorState.Select(new int[0]);
        }
    }
}
#endif
