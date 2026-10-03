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
    /// Scenario "editor_redesign": the view-first window, Quick add, the wheel, the themes, the keymap,
    /// the Keys window and the command search, hide and lock, and the Figma-like edits (align, spread,
    /// mirror, copy in a row, Shift + arrow). Keyboard and pad where both exist.
    /// </summary>
    internal static partial class AutoTest
    {
        private static IEnumerator TestEditorRedesign(Player player)
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
            yield return RedesignQuickAdd();
            yield return RedesignWheel();
            yield return RedesignTheme(document);
            RedesignKeymap();
            yield return RedesignWindows();
            RedesignHideLock(document);
            RedesignShapes(document);
            EditorSession.Close();
        }

        /// <summary>The view fills the window; the cards fold with Alt+1, Alt+2 and come back; Ctrl+\ and L2 + L3 hide everything.</summary>
        private static IEnumerator RedesignLayout()
        {
            yield return Frames(2);
            var root = EditorWindow.Root.rect;
            var view = EditorWindow.ViewportHost.rect;
            Check(Mathf.Abs(view.width - root.width) < 1f && Mathf.Abs(view.height - root.height) < 1f,
                $"the 3D view fills the whole window: {view.width:0}x{view.height:0} of {root.width:0}x{root.height:0}");

            EditorWindow.SetLayers(false);
            EditorWindow.SetInspector(true);
            yield return null;
            Check(!EditorWindow.LeftShown && EditorWindow.LeftPanel.anchoredPosition.x < -1000f,
                $"a folded Layers card is slid off the screen (x {EditorWindow.LeftPanel.anchoredPosition.x:0})");

            Bindings.Press(KeyCode.Alpha1, KeyMods.Alt);
            Bindings.Press(KeyCode.Alpha2, KeyMods.Alt);
            yield return null;
            Check(EditorWindow.LayersOpen && !EditorWindow.InspectorOpen
                && Mathf.Abs(EditorWindow.LeftPanel.anchoredPosition.x) < 0.5f && EditorWindow.RightPanel.anchoredPosition.x > 1000f,
                "Alt+1 unfolds Layers and Alt+2 folds the Inspector");
            Bindings.Press(KeyCode.Alpha2, KeyMods.Alt);
            Check(EditorWindow.InspectorOpen, "Alt+2 again brings the Inspector back");

            EditorState.Select(new int[0]);
            Bindings.Press(KeyCode.Backslash, KeyMods.Ctrl);
            yield return null;
            Check(EditorWindow.UiHidden && EditorWindow.TopBar.anchoredPosition.y > 1000f && !EditorWindow.LeftShown && !EditorWindow.RightShown,
                "Ctrl+\\ hides the top bar and both cards");
            Check(Bindings.Cancel() && !EditorWindow.UiHidden && ModUi.Open, "Esc brings the interface back and keeps the window open");

            _pad = new PadState();
            PadReader.Fake = _pad;
            yield return Frames(3);
            EditorState.CancelMode();
            yield return Tap(PadButton.L2, PadButton.L3);
            Check(EditorWindow.UiHidden && !FocusNav.Active, "L2 + L3 on the pad hides the interface, no panel walk");
            yield return Tap(PadButton.L2, PadButton.L3);
            Check(!EditorWindow.UiHidden, "L2 + L3 again brings it back");
            PadReader.Fake = null;
            _pad = null;
            yield return Frames(2);
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
            Palette.SetSearch("woodwall");
            yield return Frames(2);
            Check(Palette.ShownCount >= 1, $"the search finds the wall: {Palette.ShownCount} shown");
            Palette.PieceChosen?.Invoke(wall);
            yield return null;
            Check(!QuickAdd.IsOpen && EditorState.Mode == EditMode.Place && EditorState.Held == wall,
                "a piece picked in Quick add goes in hand and the popup closes");
            Check(PieceMemory.Recent.Count > 0 && PieceMemory.Recent[0] == "woodwall", "and it heads the Recent list");
            EditorState.CancelMode();

            QuickAdd.Open();
            yield return Frames(2);
            Palette.SetSearch("");
            Palette.ToggleStar(wall);
            Palette.SetTag(Palette.FavouriteKey);
            Check(PieceMemory.IsFavourite("woodwall") && Palette.ShownCount == 1,
                $"a starred piece is the only one under Starred: {Palette.ShownCount} shown");
            Palette.SetTag(Palette.RecentKey);
            Check(Palette.ShownCount >= 1, $"Recent lists what was used: {Palette.ShownCount} shown");
            Palette.ToggleStar(wall);
            Palette.SetTag(null);
            Check(!PieceMemory.IsFavourite("woodwall"), "a second right click takes the star off");
            Check(Bindings.Cancel() && !QuickAdd.IsOpen, "Esc closes Quick add");
        }

        /// <summary>One wheel notch scrolls the palette grid by its step, whatever units the platform sends.</summary>
        private static IEnumerator RedesignWheel()
        {
            QuickAdd.Open();
            Palette.SetSearch("");
            yield return Frames(3);
            var grid = EditorWindow.PalettePane.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(s => s.name == "Grid");
            var wheel = grid != null ? grid.GetComponent<WheelScroll>() : null;
            Check(wheel != null, "the palette grid scrolls with WheelScroll");
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
            var expected = Mathf.Min(wheel.Step, room - start);
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
