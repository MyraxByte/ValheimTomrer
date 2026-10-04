using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimTomrer.Editor.Placement;
using ValheimTomrer.Editor.Ui;
using ValheimTomrer.Editor.View;

namespace ValheimTomrer.Editor.Input
{
    /// <summary>What was held down with a key.</summary>
    [Flags]
    internal enum KeyMods
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Cmd = 4,
        Alt = 8,
    }

    /// <summary>One line of the help table.</summary>
    internal sealed class HelpRow
    {
        public readonly string Keys;
        public readonly string What;

        public HelpRow(string keys, string what)
        {
            Keys = keys;
            What = what;
        }
    }

    /// <summary>
    /// The one place that says what a key, the wheel and the fly keys do. The same map as the
    /// Tomrer editor's view/Editor.ts, with the help table next to it so the two cannot drift.
    ///
    /// <see cref="Press"/> is the whole dispatcher: it takes a key and what was held with it and
    /// returns whether the editor used it. <see cref="Tick"/> only reads the real keyboard and
    /// calls it, which is what lets the autotest drive every binding with no keyboard at all.
    /// </summary>
    internal static class Bindings
    {
        /// <summary>How far one arrow-key press moves the selection, and with Alt.</summary>
        public const float NudgeStep = 0.5f;

        public const float NudgeFine = 0.1f;

        /// <summary>
        /// Keys the editor watches whatever the keymap says: the ones the panel walk and the dialogs use.
        /// The keymap adds its own (<see cref="Keymap.Keys"/>).
        /// </summary>
        private static readonly KeyCode[] Always =
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Tab, KeyCode.Return, KeyCode.KeypadEnter,
        };

        private static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        private static readonly List<KeyCode> LetGo = new List<KeyCode>();
        private static KeyCode[] _readable;
        private static int _readableVersion = -1;
        private static KeyCode[] _capturable;

        /// <summary>What was held with the last key, or what the last <see cref="Tick"/> read.</summary>
        public static KeyMods Mods { get; private set; }

        public static bool Shift => (Mods & KeyMods.Shift) != 0;

        /// <summary>Ctrl or Cmd: the shortcut modifier, whichever the keyboard has.</summary>
        public static bool Shortcut => (Mods & (KeyMods.Ctrl | KeyMods.Cmd)) != 0;

        /// <summary>The fly keys held right now: right, up, forward.</summary>
        public static Vector3 Wish
        {
            get
            {
                var wish = Vector3.zero;
                wish.z += Down(Act.FlyForward) - Down(Act.FlyBack);
                wish.x += Down(Act.FlyRight) - Down(Act.FlyLeft);
                wish.y += Down(Act.FlyUp) - Down(Act.FlyDown);
                return wish;
            }
        }

        /// <summary>Everything the editor does with the keyboard, the mouse wheel and the pad, once a frame.</summary>
        public static void Tick()
        {
            var dt = Mathf.Min(EditorInput.Dt, 0.1f);
            SetMods(ReadMods());

            if (ModUi.JustTyping)
            {
                // A text box owns the keyboard: stop flying and read nothing. "Just" counts the
                // frame the box let go too: it reads its keys first, so the Enter that submitted
                // the box (and maybe opened "Replace the file?") must not press anything here.
                Held.Clear();
                return;
            }

            if (Keymap.Capturing != Act.None)
            {
                // The Keys window waits for a key: the next one pressed is the new binding.
                Held.Clear();
                CaptureKey();
                return;
            }

            if (Dialogs.IsOpen || FocusNav.Active || QuickAdd.IsOpen)
            {
                // A dialog, the piece menu and the walk still read keys, but nothing flies behind
                // them. A dialog only ever gets Tab, the arrows and Enter, through the walk.
                Held.Clear();
            }

            // macOS sends no key-up while Cmd is down, so a fly key held into a shortcut would
            // fly on for ever. Cmd going down drops everything, as the browser editor does.
            if (ZInput.GetKeyDown(KeyCode.LeftCommand, false) || ZInput.GetKeyDown(KeyCode.RightCommand, false))
            {
                Held.Clear();
            }

            foreach (var key in Readable())
            {
                if (ZInput.GetKeyDown(key, false))
                {
                    Press(key, Mods);
                }
            }

            LetGo.Clear();
            foreach (var key in Held)
            {
                if (!ZInput.GetKey(key, false))
                {
                    LetGo.Add(key);
                }
            }

            foreach (var key in LetGo)
            {
                Held.Remove(key);
            }

            Fly(dt);
        }

        /// <summary>
        /// One key press. True when the editor used it. Everything the help table promises runs
        /// through here, so the test can press a key without a keyboard.
        /// </summary>
        public static bool Press(KeyCode key, KeyMods mods)
        {
            SetMods(mods);

            // A text box has the keyboard: the key is not ours.
            if (ModUi.Typing)
            {
                return false;
            }

            // A dialog covers everything else: only the walk through it reads a key, so Tab, the
            // arrows and Enter reach its buttons and nothing else does.
            if (Dialogs.IsOpen)
            {
                return DialogKey(key);
            }

            var act = Keymap.Match(key, mods);

            // Quick add has the keyboard while it is up. Its key closes it, the arrows move the light,
            // Enter places, and any other key that did not reach the search box puts the keyboard back
            // in it, so the next letters are typed.
            if (QuickAdd.IsOpen)
            {
                switch (act == Act.QuickAdd ? KeyCode.None : key)
                {
                    case KeyCode.None:
                        QuickAdd.Close();
                        break;
                    case KeyCode.UpArrow:
                        QuickAdd.Move(0, -1);
                        break;
                    case KeyCode.DownArrow:
                        QuickAdd.Move(0, 1);
                        break;
                    case KeyCode.LeftArrow:
                        QuickAdd.Move(-1, 0);
                        break;
                    case KeyCode.RightArrow:
                        QuickAdd.Move(1, 0);
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        QuickAdd.Pick();
                        break;
                    default:
                        QuickAdd.FocusSearch();
                        break;
                }

                return true;
            }

            // The two keys that open and step the panel walk work from inside it too.
            if (act == Act.QuickAdd || act == Act.Walk || act == Act.WalkBack)
            {
                return Run(act);
            }

            // The walk owns the keyboard while it is on, the same way the piece menu does: the
            // arrows move, Enter presses, and the editing keys stand back. Esc leaves it.
            if (FocusNav.Active)
            {
                FocusKey(key);
                return true;
            }

            if (act != Act.None && Run(act))
            {
                // Ctrl went down first as the fly-down key; with a shortcut after it, it was a modifier.
                if ((mods & KeyMods.Ctrl) != 0)
                {
                    Held.Remove(KeyCode.LeftControl);
                    Held.Remove(KeyCode.RightControl);
                }

                return true;
            }

            // Cmd is never a fly modifier: Cmd + a fly key is a shortcut that missed.
            if ((mods & KeyMods.Cmd) == 0 && Keymap.IsFlyKey(key))
            {
                Held.Add(key);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Does what an action says. True when it was used; false for the few that only apply in one
        /// mode (the snap keys outside placing), so the key falls through.
        /// </summary>
        public static bool Run(Act act)
        {
            var placing = EditorState.Mode == EditMode.Place;
            switch (act)
            {
                case Act.Undo:
                    EditorState.Undo();
                    return true;
                case Act.Redo:
                    EditorState.Redo();
                    return true;
                case Act.SelectAll:
                    EditorState.SelectAll();
                    return true;
                case Act.Duplicate:
                    EditorState.StartDuplicate();
                    return true;
                case Act.Save:
                    EditorCommands.Save();
                    return true;
                case Act.Delete:
                    if (!placing)
                    {
                        EditorState.DeleteSelection();
                    }

                    return true;
                case Act.Move:
                    if (!placing)
                    {
                        EditorState.StartMove();
                    }

                    return true;
                case Act.RotateRight:
                case Act.RotateLeft:
                    var direction = act == Act.RotateLeft ? -1 : 1;
                    if (placing)
                    {
                        EditorState.SetPlaceSteps(EditorState.Steps + direction);
                    }
                    else
                    {
                        EditorState.RotateSelection(direction);
                    }

                    return true;
                case Act.NudgeForward:
                case Act.NudgeBack:
                case Act.NudgeLeft:
                case Act.NudgeRight:
                    if (!placing)
                    {
                        NudgeGround(act);
                    }

                    return true;
                case Act.NudgeUp:
                case Act.NudgeDown:
                    if (!placing)
                    {
                        EditorState.Nudge(new Vector3(0f, act == Act.NudgeUp ? Step() : -Step(), 0f));
                    }

                    return true;
                case Act.Frame:
                    ViewportHost.Frame();
                    return true;
                case Act.ViewFront:
                    ViewportHost.ShowView(ViewPreset.Front);
                    return true;
                case Act.ViewBack:
                    ViewportHost.ShowView(ViewPreset.Back);
                    return true;
                case Act.ViewRight:
                    ViewportHost.ShowView(ViewPreset.Right);
                    return true;
                case Act.ViewLeft:
                    ViewportHost.ShowView(ViewPreset.Left);
                    return true;
                case Act.ViewTop:
                    ViewportHost.ShowView(ViewPreset.Top);
                    return true;
                case Act.ViewBottom:
                    ViewportHost.ShowView(ViewPreset.Bottom);
                    return true;
                case Act.ViewIso:
                    ViewportHost.ShowView(ViewPreset.Iso);
                    return true;
                case Act.ToggleOrtho:
                    ViewportHost.ToggleOrtho();
                    return true;
                case Act.Isolate:
                    EditorState.IsolateSelection();
                    return true;
                case Act.Copy:
                    EditorState.CopySelection();
                    return true;
                case Act.Paste:
                    EditorState.StartPaste();
                    return true;
                case Act.Group:
                    EditorState.GroupSelection();
                    return true;
                case Act.Ungroup:
                    EditorState.UngroupSelection();
                    return true;
                case Act.CloseGap:
                    EditorMeasure.CloseGap();
                    return true;
                case Act.DropToFloor:
                    EditorState.DropToFloor();
                    return true;
                case Act.NextProblem:
                    ChecksPage.SelectNext();
                    return true;
                case Act.ToggleIso:
                    ViewportHost.ToggleIso();
                    return true;
                case Act.TurnViewLeft:
                    ViewportHost.TurnView(-1);
                    return true;
                case Act.TurnViewRight:
                    ViewportHost.TurnView(1);
                    return true;
                case Act.FloorUp:
                    EditorState.FloorUp();
                    return true;
                case Act.FloorDown:
                    EditorState.FloorDown();
                    return true;
                case Act.ShowAllFloors:
                    EditorState.ShowAllFloors();
                    return true;
                case Act.TimeOfDay:
                    EditorCommands.CycleTimeOfDay();
                    return true;
                case Act.ToggleSupportColours:
                    EditorCommands.ToggleSupportColours();
                    return true;
                case Act.SaveSelectionAs:
                    EditorCommands.SaveSelectionDialog();
                    return true;
                case Act.InsertBlueprint:
                    EditorCommands.InsertDialog();
                    return true;
                case Act.Ruler:
                    ViewportHost.ToggleRuler();
                    return true;
                case Act.ToggleDimensions:
                    EditorCommands.ToggleDimensions();
                    return true;
                case Act.BookmarkAdd:
                    ViewportHost.AddBookmark();
                    return true;
                case Act.BookmarkNext:
                    ViewportHost.NextBookmark();
                    return true;
                case Act.BookmarkClear:
                    ViewportHost.ClearBookmarks();
                    return true;
                case Act.MouseLook:
                    // Hand the mouse to the pane, so it looks around instead of pointing. Esc
                    // gives it back. A click never does this: it selects.
                    ViewportHost.Capture();
                    return true;
                case Act.SnapPrev:
                case Act.SnapNext:
                    if (!placing)
                    {
                        return false;
                    }

                    EditorState.SetManualSnap(EditorState.Manual + (act == Act.SnapPrev ? -1 : 1));
                    return true;
                case Act.Help:
                    Dialogs.Help();
                    return true;
                case Act.QuickAdd:
                    // From the panel walk it leaves the walk first.
                    FocusNav.Leave();
                    QuickAdd.Toggle();
                    return true;
                case Act.Walk:
                case Act.WalkBack:
                    if (FocusNav.Active)
                    {
                        FocusNav.Move(act == Act.WalkBack ? -1 : 1);
                    }
                    else
                    {
                        FocusNav.Enter();
                    }

                    return true;
                case Act.ToggleLayers:
                    EditorWindow.SetLayers(!EditorWindow.LayersOpen);
                    return true;
                case Act.ToggleInspector:
                    EditorWindow.SetInspector(!EditorWindow.InspectorOpen);
                    return true;
                case Act.HideUi:
                    EditorWindow.SetUiHidden(!EditorWindow.UiHidden);
                    return true;
                case Act.HideSelection:
                    EditorState.HideSelection();
                    return true;
                case Act.LockSelection:
                    EditorState.LockSelection();
                    return true;
                case Act.ShowAll:
                    EditorState.ShowAll();
                    return true;
                case Act.SelectSimilar:
                    EditorState.SelectSimilar();
                    return true;
                case Act.AlignAxis:
                    EditorState.CycleAlignAxis();
                    return true;
                case Act.AlignMin:
                    EditorState.AlignSelection(-1);
                    return true;
                case Act.AlignMid:
                    EditorState.AlignSelection(0);
                    return true;
                case Act.AlignMax:
                    EditorState.AlignSelection(1);
                    return true;
                case Act.Spread:
                    EditorState.SpreadSelection();
                    return true;
                case Act.MirrorX:
                    EditorState.MirrorSelection(0);
                    return true;
                case Act.MirrorZ:
                    EditorState.MirrorSelection(2);
                    return true;
                case Act.CopyInRow:
                    EditorState.CopyInRow(CameraRight());
                    return true;
                case Act.Commands:
                    Dialogs.Commands();
                    return true;
                case Act.CycleGrid:
                    EditorCommands.CycleGrid();
                    return true;
                case Act.CycleAngle:
                    EditorCommands.CycleAngle();
                    return true;
                case Act.ToggleSnapPoints:
                    EditorCommands.ToggleSnapPoints();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A key pressed while the Keys window waits: Esc is the ladder's, Backspace clears, the rest binds.</summary>
        private static void CaptureKey()
        {
            _capturable = _capturable ?? Capturable();
            foreach (var key in _capturable)
            {
                if (!ZInput.GetKeyDown(key, false))
                {
                    continue;
                }

                var act = Keymap.Capturing;

                // The keys that open the editor and the capture are not for actions: they would run it and close the window.
                if (key == EditorConfig.Key.Value || key == EditorConfig.CaptureKey.Value)
                {
                    continue;
                }

                // Ctrl is a modifier for every action but the fly keys: it must not end the capture before the key it goes with.
                if ((key == KeyCode.LeftControl || key == KeyCode.RightControl) && act < Act.FlyForward)
                {
                    continue;
                }

                if (key == KeyCode.Backspace && (Mods & (KeyMods.Ctrl | KeyMods.Alt | KeyMods.Shift | KeyMods.Cmd)) == 0)
                {
                    Keymap.Clear(act);
                    Dialogs.RefreshKeys();
                    Toasts.Info($"{Keymap.Of(act).Label}: no key.");
                    return;
                }

                var taken = Keymap.Capture(key, Mods);
                Dialogs.RefreshKeys();
                Toasts.Info(taken == null
                    ? $"{Keymap.Of(act).Label}: {Keymap.Describe(act)}."
                    : $"{Keymap.Of(act).Label}: {Keymap.Describe(act)}. Taken from {taken}.");
                return;
            }
        }

        private static KeyCode[] Capturable()
        {
            var works = new List<KeyCode>();
            foreach (var key in Keymap.BindableKeys)
            {
                try
                {
                    ZInput.GetKeyDown(key, false);
                    works.Add(key);
                }
                catch (Exception)
                {
                    // the game's input has no name for it: it cannot be bound
                }
            }

            return works.ToArray();
        }

        /// <summary>
        /// The watched keys the game's input can actually read. ZInput turns a KeyCode into a
        /// new-input-system key and throws on the ones it has no name for, so each is tried once.
        /// </summary>
        private static KeyCode[] Readable()
        {
            if (_readable != null && _readableVersion == Keymap.Version)
            {
                return _readable;
            }

            var wanted = new HashSet<KeyCode>(Always);
            foreach (var key in Keymap.Keys())
            {
                wanted.Add(key);
            }

            var works = new List<KeyCode>(wanted.Count);
            foreach (var key in wanted)
            {
                try
                {
                    ZInput.GetKeyDown(key, false);
                    works.Add(key);
                }
                catch (Exception e)
                {
                    ValheimTomrerPlugin.Log.LogInfo($"the game's input cannot read {key} ({e.GetType().Name}), skipping it");
                }
            }

            _readable = works.ToArray();
            _readableVersion = Keymap.Version;
            return _readable;
        }

        /// <summary>A key let go. Only the fly keys care.</summary>
        public static void Release(KeyCode key)
        {
            Held.Remove(key);
        }

        /// <summary>
        /// What is held down with the keys. Shift turns snapping off while placing, the way the
        /// game does, so it has to be known even when no key is pressed this frame.
        /// </summary>
        public static void SetMods(KeyMods mods)
        {
            Mods = mods;
            EditorState.Snapping = (mods & KeyMods.Shift) == 0;
        }

        /// <summary>Moves the camera from the held fly keys. Shift flies three times faster.</summary>
        public static void Fly(float dt)
        {
            var camera = ViewportHost.Camera;
            if (camera == null)
            {
                return;
            }

            camera.FlyKeys(Wish, Shift, dt);
        }

        /// <summary>
        /// The wheel over the pane: it turns what is in hand, like the game's build wheel, and
        /// zooms toward the cursor otherwise. One notch is one step of 22.5 degrees.
        /// </summary>
        public static void Wheel(float notches, Vector2 viewport)
        {
            var camera = ViewportHost.Camera;
            if (camera == null)
            {
                return;
            }

            if (EditorState.Mode == EditMode.Place)
            {
                var steps = Mathf.RoundToInt(Mathf.Sign(notches) * Mathf.Ceil(Mathf.Abs(notches)));
                if (steps != 0)
                {
                    EditorState.SetPlaceSteps(EditorState.Steps + steps);
                }

                return;
            }

            // The UI module reports one notch as 1; the zoom curve is written for the browser's 100.
            camera.Zoom(-notches * 100f, viewport);
        }

        /// <summary>
        /// Esc (and the pad's circle), one step back at a time: a dialog, the piece menu, what is
        /// in hand, the mouse the pane took, the panel walk, then the selection. False means there
        /// was nothing left, so the window closes.
        /// </summary>
        public static bool Cancel()
        {
            if (Keymap.CancelCapture())
            {
                Dialogs.RefreshKeys();
                return true;
            }

            if (Dialogs.Dismiss())
            {
                return true;
            }

            if (Header.CloseMenu())
            {
                return true;
            }

            if (QuickAdd.Close())
            {
                return true;
            }

            if (EditorState.Mode != EditMode.Idle)
            {
                EditorState.CancelMode();
                return true;
            }

            if (ViewportHost.RulerOn)
            {
                ViewportHost.SetRuler(false);
                return true;
            }

            if (ViewportHost.Release())
            {
                return true;
            }

            if (FocusNav.Active)
            {
                FocusNav.Leave();
                return true;
            }

            if (EditorState.SelectionCount > 0)
            {
                EditorState.Select(Array.Empty<int>());
                return true;
            }

            // The interface was hidden: Esc brings it back before it closes the editor.
            if (EditorWindow.UiHidden)
            {
                EditorWindow.SetUiHidden(false);
                return true;
            }

            return false;
        }

        /// <summary>Forget the held keys, so a key held while the window opened does not fly.</summary>
        public static void Reset()
        {
            Held.Clear();
            Mods = KeyMods.None;
        }

        // ---------- the map ----------

        /// <summary>
        /// A dialog is up: Tab and the arrows walk what it holds and Enter presses it. Everything
        /// else is left alone, so the game never sees it and the editor never acts on it.
        /// </summary>
        private static bool DialogKey(KeyCode key)
        {
            if (key == KeyCode.Tab)
            {
                if (FocusNav.InDialog)
                {
                    FocusNav.Move(Shift ? -1 : 1);
                }
                else
                {
                    FocusNav.EnterDialog();
                }

                return true;
            }

            if (!FocusNav.InDialog)
            {
                return false;
            }

            switch (key)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    FocusKey(key);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The keys the panel walk uses. The rest are swallowed while it is on.</summary>
        private static void FocusKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.UpArrow:
                    FocusNav.Step(0, 1);
                    break;
                case KeyCode.DownArrow:
                    FocusNav.Step(0, -1);
                    break;
                case KeyCode.LeftArrow:
                    FocusNav.Step(-1, 0);
                    break;
                case KeyCode.RightArrow:
                    FocusNav.Step(1, 0);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    FocusNav.Press();
                    break;
            }
        }

        /// <summary>A nudge key moves along the ground axis closest to where the camera looks.</summary>
        private static void NudgeGround(Act act)
        {
            var raycast = ViewportHost.Raycast;
            var forward = Vector3.forward;
            var right = Vector3.right;
            if (raycast != null)
            {
                raycast.GroundAxes(out forward, out right);
            }

            var axis = act == Act.NudgeForward ? Snap(forward)
                : act == Act.NudgeBack ? -Snap(forward)
                : act == Act.NudgeRight ? Snap(right)
                : -Snap(right);
            EditorState.Nudge(axis * Step());
        }

        /// <summary>The camera's right along the ground, for Copy in a row.</summary>
        public static Vector3 CameraRight()
        {
            var forward = Vector3.forward;
            var right = Vector3.right;
            ViewportHost.Raycast?.GroundAxes(out forward, out right);
            return right;
        }

        /// <summary>The whole ground axis a direction is closest to.</summary>
        private static Vector3 Snap(Vector3 direction)
        {
            return Mathf.Abs(direction.x) >= Mathf.Abs(direction.z)
                ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }

        /// <summary>One nudge: 0.5 m, with Alt 0.1 m, with Shift four times as far (2 m), like Shift + arrow in Figma.</summary>
        private static float Step()
        {
            var step = EditorState.GridStep > 0f ? EditorState.GridStep : NudgeStep;
            return (Mods & KeyMods.Alt) != 0 ? NudgeFine : (Mods & KeyMods.Shift) != 0 ? step * 4f : step;
        }

        /// <summary>1 while any key of the fly action is held, else 0.</summary>
        private static float Down(Act act)
        {
            foreach (var key in Keymap.FlyKeys(act))
            {
                if (Held.Contains(key))
                {
                    return 1f;
                }
            }

            return 0f;
        }

        private static KeyMods ReadMods()
        {
            var mods = KeyMods.None;
            if (ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false))
            {
                mods |= KeyMods.Shift;
            }

            if (ZInput.GetKey(KeyCode.LeftControl, false) || ZInput.GetKey(KeyCode.RightControl, false))
            {
                mods |= KeyMods.Ctrl;
            }

            if (ZInput.GetKey(KeyCode.LeftCommand, false) || ZInput.GetKey(KeyCode.RightCommand, false))
            {
                mods |= KeyMods.Cmd;
            }

            if (ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false))
            {
                mods |= KeyMods.Alt;
            }

            return mods;
        }

        // ---------- the help table ----------

        /// <summary>
        /// The mouse and keyboard half of the help. The mouse rows are fixed; the key rows are read
        /// from the keymap, so the help always says what the keys do now.
        /// </summary>
        public static HelpRow[] Keys
        {
            get
            {
                var rows = new List<HelpRow>
                {
                    new HelpRow("Click", "Select a piece, or drop what is in hand. Shift+click adds or removes."),
                    new HelpRow("Right drag", "Look around. The cursor stays where it is."),
                    new HelpRow("Middle drag, or Shift + right drag", "Pan"),
                    new HelpRow("Wheel", "Zoom toward the cursor. While placing it turns the piece 22.5 degrees."),
                    new HelpRow("Drag on the view", "Select everything in the box. Only while the cursor is free."),
                };

                rows.Add(new HelpRow(
                    string.Join(", ", new[] { Act.FlyForward, Act.FlyLeft, Act.FlyBack, Act.FlyRight }.Select(Keymap.Describe)),
                    "Fly forward (where the camera looks), left, back, right"));
                rows.Add(new HelpRow(
                    Keymap.Describe(Act.FlyUp) + ", " + Keymap.Describe(Act.FlyDown),
                    "Fly up, down. Hold Shift to fly 3 times faster."));
                foreach (var def in Keymap.All)
                {
                    if (def.Id < Act.FlyForward)
                    {
                        rows.Add(new HelpRow(Keymap.Describe(def.Id), def.Help));
                    }
                }

                rows.Add(new HelpRow("Shift (hold)", "No snapping while held, like in the game"));
                rows.Add(new HelpRow("Arrow keys, Enter (while walking)",
                    "Step to the widget above, below, left or right, and press it. A text box starts "
                    + "typing, Esc gives it back."));
                rows.Add(new HelpRow("Tab, Shift+Tab (in a dialog)", "Walk what the dialog holds"));
                rows.Add(new HelpRow("Esc",
                    "Close the dialog or Quick add, else stop placing, else give the mouse back, else leave the "
                    + "walk, else clear the selection, else bring the interface back, else close the editor"));
                return rows.ToArray();
            }
        }

        /// <summary>The controller half, in the wording of the pad in hand (<see cref="PadBindings"/>).</summary>
        public static HelpRow[] Pad => PadBindings.Help(EditorInput.Glyphs);
    }
}
