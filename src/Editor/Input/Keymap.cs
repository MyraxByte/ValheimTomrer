using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimTomrer.Editor.Input
{
    /// <summary>Everything a key can do in the editor. <see cref="Bindings.Run"/> does each of them.</summary>
    internal enum Act
    {
        None,
        Undo,
        Redo,
        SelectAll,
        Duplicate,
        Save,
        Delete,
        Move,
        RotateRight,
        RotateLeft,
        NudgeForward,
        NudgeBack,
        NudgeLeft,
        NudgeRight,
        NudgeUp,
        NudgeDown,
        Frame,
        ViewFront,
        ViewBack,
        ViewRight,
        ViewLeft,
        ViewTop,
        ViewBottom,
        ViewIso,
        ToggleOrtho,
        Isolate,
        MouseLook,
        SnapPrev,
        SnapNext,
        Help,
        QuickAdd,
        Walk,
        WalkBack,
        ToggleLayers,
        ToggleInspector,
        HideUi,
        HideSelection,
        LockSelection,
        ShowAll,
        SelectSimilar,
        AlignMin,
        AlignMid,
        AlignMax,
        Spread,
        AlignAxis,
        MirrorX,
        MirrorZ,
        CopyInRow,
        Commands,
        CycleGrid,
        CycleAngle,
        ToggleSnapPoints,
        Copy,
        Paste,
        Group,
        Ungroup,
        CloseGap,
        DropToFloor,
        ToggleIso,
        TurnViewLeft,
        TurnViewRight,
        FloorUp,
        FloorDown,
        ShowAllFloors,
        TimeOfDay,
        NextProblem,
        Ruler,
        ToggleDimensions,
        BookmarkAdd,
        BookmarkNext,
        BookmarkClear,
        FlyForward,
        FlyBack,
        FlyLeft,
        FlyRight,
        FlyUp,
        FlyDown,
    }

    /// <summary>A key and the modifiers that must be held with it. Ctrl also stands for Cmd.</summary>
    internal struct Chord
    {
        public KeyCode Key;
        public KeyMods Mods;

        public Chord(KeyCode key, KeyMods mods)
        {
            Key = key;
            Mods = mods;
        }

        public override string ToString()
        {
            var text = "";
            if ((Mods & KeyMods.Ctrl) != 0)
            {
                text += "Ctrl+";
            }

            if ((Mods & KeyMods.Alt) != 0)
            {
                text += "Alt+";
            }

            if ((Mods & KeyMods.Shift) != 0)
            {
                text += "Shift+";
            }

            return text + Keymap.KeyName(Key);
        }
    }

    /// <summary>One action: its name in the config, the words for people, and the keys of each preset.</summary>
    internal sealed class ActionDef
    {
        public Act Id;
        public string Name;
        public string Label;
        public string Group;
        public string Help;
        public string Tomrer;
        public string Figma;
        public string Blender;
        public ConfigEntry<string> Entry;

        public string For(string preset)
        {
            return preset == "Figma" ? Figma : preset == "Blender" ? Blender : Tomrer;
        }
    }

    /// <summary>
    /// The one table of what the keyboard does. A preset (Tomrer, Figma, Blender) gives every action
    /// its keys, and each action has a setting in the config file (section Keys) that replaces them:
    /// empty means the preset's keys, "none" means no key, else a list like "Ctrl+Z, Ctrl+Y". The
    /// Keys window changes the same settings, so editing the file by hand and clicking agree.
    ///
    /// Esc, Enter and the arrows inside a dialog or the panel walk are not in the table: they are the
    /// editor's own way of backing out and stepping, and cannot be taken away.
    /// </summary>
    internal static class Keymap
    {
        public static readonly string[] Presets = { "Tomrer", "Figma", "Blender" };

        public const string GroupView = "Interface and camera";
        public const string GroupEdit = "Editing";
        public const string GroupFly = "Flying";

        private static readonly ActionDef[] Table =
        {
            Def(Act.QuickAdd, "QuickAdd", "Quick add (the list of pieces)", GroupView,
                "Quick add: the list of pieces over the view. Type to search, click a piece to place it, right click stars it.",
                "Tab", "Tab, Shift+I", "Shift+A, Tab"),
            Def(Act.ToggleLayers, "ToggleLayers", "Layers card", GroupView,
                "Fold the Layers card (the pieces of the blueprint) in or out", "Alt+1", "Alt+1", "Alt+1"),
            Def(Act.ToggleInspector, "ToggleInspector", "Inspector card", GroupView,
                "Fold the Inspector card (blueprint, selection, problems) in or out", "Alt+2", "Alt+2", "Alt+2"),
            Def(Act.HideUi, "HideUi", "Hide the interface", GroupView,
                "Hide the whole interface and keep the view. The same keys, or Esc, bring it back.",
                "Ctrl+\\", "Ctrl+\\", "Ctrl+\\"),
            Def(Act.Frame, "Frame", "Look at the selection", GroupView,
                "Look at the selection, or at everything", "F", "F, Shift+2", "Home, Num."),
            Def(Act.ViewFront, "ViewFront", "View: front", GroupView,
                "Look at the blueprint from the front (the camera keeps its distance and what it looks at)",
                "1", "1", "Keypad1"),
            Def(Act.ViewBack, "ViewBack", "View: back", GroupView, "Look from the back", "Ctrl+1", "Ctrl+1", "Ctrl+Keypad1"),
            Def(Act.ViewRight, "ViewRight", "View: right", GroupView, "Look from the right", "2", "2", "Keypad3"),
            Def(Act.ViewLeft, "ViewLeft", "View: left", GroupView, "Look from the left", "Ctrl+2", "Ctrl+2", "Ctrl+Keypad3"),
            Def(Act.ViewTop, "ViewTop", "View: top", GroupView, "Look straight down, like a floor plan", "3", "3", "Keypad7"),
            Def(Act.ViewBottom, "ViewBottom", "View: bottom", GroupView, "Look straight up from under the blueprint",
                "Ctrl+3", "Ctrl+3", "Ctrl+Keypad7"),
            Def(Act.ViewIso, "ViewIso", "View: corner", GroupView, "Look from a corner, a bit above, like the Frame key", "4", "4", "Keypad0"),
            Def(Act.ToggleIso, "ToggleIso", "Isometric view", GroupView,
                "Isometric mode: a flat view at a fixed angle for building without free 3D. WASD slide the view, "
                + "the wheel zooms, [ and ] turn it a quarter, Ctrl + Up and Down change the floor.",
                "6", "6", "Keypad2"),
            Def(Act.TurnViewLeft, "TurnViewLeft", "Turn the view left", GroupView,
                "Turn the view a quarter to the left round the point it looks at", "[", "[", "Keypad4"),
            Def(Act.TurnViewRight, "TurnViewRight", "Turn the view right", GroupView,
                "Turn the view a quarter to the right round the point it looks at", "]", "]", "Keypad6"),
            Def(Act.FloorUp, "FloorUp", "Floor up", GroupView,
                "Go up a floor: what is above it is hidden, new pieces go on its level", "Ctrl+Up", "Ctrl+Up", "Ctrl+Up"),
            Def(Act.FloorDown, "FloorDown", "Floor down", GroupView,
                "Go down a floor (from all floors: to the top one)", "Ctrl+Down", "Ctrl+Down", "Ctrl+Down"),
            Def(Act.ShowAllFloors, "ShowAllFloors", "Show all floors", GroupView,
                "Show every floor again", "Ctrl+0", "Ctrl+0", "Ctrl+0"),
            Def(Act.TimeOfDay, "TimeOfDay", "Time of day", GroupView,
                "The light in the view: morning, day, evening, night. Only the view, not the blueprint.",
                "Alt+L", "Alt+L", "Alt+L"),
            Def(Act.ToggleOrtho, "ToggleOrtho", "Perspective and orthographic", GroupView,
                "Switch between perspective and a flat orthographic view, where parallel lines stay parallel",
                "5", "5", "Keypad5"),
            Def(Act.Isolate, "Isolate", "Show only the selection", GroupEdit,
                "Hide every piece that is not selected, to work inside a room. Again, or with nothing selected, shows all.",
                "I", "Alt+I", "KeypadDivide"),
            Def(Act.MouseLook, "MouseLook", "Hold the mouse in the view", GroupView,
                "Hold the mouse in the view, so it looks around like flying in the game. Esc gives the cursor back.",
                "C", "C", "C"),
            Def(Act.Walk, "Walk", "Walk the panels", GroupView,
                "Walk the top bar and the two cards, on", "F6", "F6", "F6"),
            Def(Act.WalkBack, "WalkBack", "Walk the panels, back", GroupView,
                "Walk the top bar and the two cards, back", "Shift+F6", "Shift+F6", "Shift+F6"),
            Def(Act.Commands, "Commands", "Search commands", GroupView,
                "Search every command by name and run it", "Ctrl+K", "Ctrl+/, Ctrl+K", "F3"),
            Def(Act.Help, "Help", "Help", GroupView,
                "This help, and the Keys window", "H, Question, Shift+/", "H, Question, Shift+/", "F1, Question, Shift+/"),

            Def(Act.CycleGrid, "CycleGrid", "Grid step", GroupEdit,
                "The next placing grid: off, 0.25, 0.5, 1, 2, 4 m. Free pieces land on it, the arrows move this far.",
                "Alt+G", "Alt+G", "Alt+G"),
            Def(Act.CycleAngle, "CycleAngle", "Turn step", GroupEdit,
                "The next turn step: 22.5 (the game's), 45, 90, 5, 15 degrees", "Alt+R", "Alt+R", "Alt+R"),
            Def(Act.ToggleSnapPoints, "ToggleSnapPoints", "Snap points on and off", GroupEdit,
                "Snap to the pieces' snap points, or not (the grid still works). Shift held turns all snapping off for a moment.",
                "Alt+S", "Alt+S", "Shift+Tab"),
            Def(Act.Copy, "Copy", "Copy", GroupEdit,
                "Copy the selection. Paste puts it in hand, in this blueprint or in another one.",
                "Ctrl+C", "Ctrl+C", "Ctrl+C"),
            Def(Act.Paste, "Paste", "Paste", GroupEdit,
                "Put what was copied in hand. It follows the mouse, click to drop, copies keep coming until Esc.",
                "Ctrl+V", "Ctrl+V", "Ctrl+V"),
            Def(Act.Group, "Group", "Group the selection", GroupEdit,
                "A click on one piece of a group picks them all. Groups are not saved in the file.",
                "Ctrl+G", "Ctrl+G", "Ctrl+G"),
            Def(Act.Ungroup, "Ungroup", "Ungroup", GroupEdit, "Break up the groups of the selection",
                "Ctrl+Shift+G", "Ctrl+Shift+G", "Ctrl+Alt+G"),
            Def(Act.CloseGap, "CloseGap", "Close the gap to the nearest piece", GroupEdit,
                "Move the selection along the align axis until it touches the nearest piece beside it",
                "Alt+Q", "Alt+Q", "Alt+Q"),
            Def(Act.DropToFloor, "DropToFloor", "Drop to the floor", GroupEdit,
                "Lower or lift the selection so its lowest point is on the floor (height 0)", "Alt+W", "Alt+W", "Alt+W"),
            Def(Act.NextProblem, "NextProblem", "Next problem", GroupView,
                "Select the pieces of the next problem in the Checks list and look at them", "Alt+P", "Alt+P", "Alt+P"),
            Def(Act.Ruler, "Ruler", "Ruler", GroupView,
                "Measure: click two points in the view, the distance and the three differences show. Click again to start over, Esc stops.",
                "M", "Ctrl+Shift+M", "M"),
            Def(Act.ToggleDimensions, "ToggleDimensions", "Sizes and gaps", GroupView,
                "Show the selection's size along its edges and the gaps to the pieces beside it",
                "Alt+Z", "Alt+Z", "Alt+Z"),
            Def(Act.BookmarkAdd, "BookmarkAdd", "Save this view", GroupView,
                "Remember where the camera is, up to 9 views. Not saved in the file.", "Ctrl+B", "Ctrl+B", "Ctrl+B"),
            Def(Act.BookmarkNext, "BookmarkNext", "Next saved view", GroupView,
                "Fly to the next saved view", "B", "B", "B"),
            Def(Act.BookmarkClear, "BookmarkClear", "Forget the saved views", GroupView,
                "Forget every saved view", "Ctrl+Shift+B", "Ctrl+Shift+B", "Ctrl+Shift+B"),
            Def(Act.SelectAll, "SelectAll", "Select all", GroupEdit, "Select all", "Ctrl+A", "Ctrl+A", "Ctrl+A"),
            Def(Act.Move, "Move", "Move the selection", GroupEdit,
                "Move the selection: it follows the mouse, click to drop, Esc to cancel", "G", "V, G", "G"),
            Def(Act.Duplicate, "Duplicate", "Duplicate", GroupEdit,
                "Duplicate: the copy follows the mouse, and copies keep coming until Esc",
                "Ctrl+D", "Ctrl+D", "Shift+D"),
            Def(Act.RotateRight, "RotateRight", "Turn right", GroupEdit,
                "Turn 22.5 degrees: the piece in hand, else the selection", "R", "R", "R"),
            Def(Act.RotateLeft, "RotateLeft", "Turn left", GroupEdit, "Turn 22.5 degrees the other way",
                "Shift+R", "Shift+R", "Shift+R"),
            Def(Act.NudgeForward, "NudgeForward", "Nudge forward", GroupEdit,
                "Nudge 0.5 m along the ground axis closest to where the camera looks (with Alt 0.1 m, with Shift 2 m)", "Up", "Up", "Up"),
            Def(Act.NudgeBack, "NudgeBack", "Nudge back", GroupEdit, "Nudge back", "Down", "Down", "Down"),
            Def(Act.NudgeLeft, "NudgeLeft", "Nudge left", GroupEdit, "Nudge left", "Left", "Left", "Left"),
            Def(Act.NudgeRight, "NudgeRight", "Nudge right", GroupEdit, "Nudge right", "Right", "Right", "Right"),
            Def(Act.NudgeUp, "NudgeUp", "Nudge up", GroupEdit, "Nudge up", "PageUp", "PageUp", "PageUp"),
            Def(Act.NudgeDown, "NudgeDown", "Nudge down", GroupEdit, "Nudge down", "PageDown", "PageDown", "PageDown"),
            Def(Act.SnapPrev, "SnapPrev", "Snap point, previous", GroupEdit,
                "While placing: pick the snap point that goes on the aimed spot, like in the game", "Q", "Q", "Q"),
            Def(Act.SnapNext, "SnapNext", "Snap point, next", GroupEdit, "While placing: the next snap point", "E", "E", "E"),
            Def(Act.SelectSimilar, "SelectSimilar", "Select the same kind", GroupEdit,
                "Select every piece of the same kind as the selection. A double click on a piece does it too.",
                "Ctrl+Shift+A", "Ctrl+Shift+A", "Shift+G"),
            Def(Act.HideSelection, "HideSelection", "Hide the selection", GroupEdit,
                "Hide the selection so you can see inside. With nothing selected it shows everything again. Not saved in the file.",
                "Ctrl+Shift+H", "Ctrl+Shift+H", "H"),
            Def(Act.LockSelection, "LockSelection", "Lock the selection", GroupEdit,
                "Lock the selection: a click or a box can no longer pick it. Not saved in the file.",
                "Ctrl+Shift+L", "Ctrl+Shift+L", "Ctrl+Shift+L"),
            Def(Act.ShowAll, "ShowAll", "Show and unlock all", GroupEdit,
                "Show every hidden piece and unlock every locked one", "Alt+Shift+H", "Alt+Shift+H", "Alt+H"),
            Def(Act.AlignAxis, "AlignAxis", "Align axis (X, Y, Z)", GroupEdit,
                "Pick the axis the next Align and Spread keys work along: X, Y or Z", "Alt+X", "Alt+X", "Alt+X"),
            Def(Act.AlignMin, "AlignMin", "Align to the low edge", GroupEdit,
                "Line the selected pieces up on the lowest edge along the align axis", "Alt+A", "Alt+A", "Alt+A"),
            Def(Act.AlignMid, "AlignMid", "Align to the middle", GroupEdit,
                "Line the selected pieces up on their middle along the align axis", "Alt+C", "Alt+H", "Alt+C"),
            Def(Act.AlignMax, "AlignMax", "Align to the high edge", GroupEdit,
                "Line the selected pieces up on the highest edge along the align axis", "Alt+D", "Alt+D", "Alt+D"),
            Def(Act.Spread, "Spread", "Spread out evenly", GroupEdit,
                "Spread three or more pieces so the gaps between them are equal. The two outer ones stay.",
                "Alt+E", "Ctrl+Alt+H", "Alt+E"),
            Def(Act.MirrorX, "MirrorX", "Mirror along X", GroupEdit,
                "Mirror the selection across its middle along X", "Shift+H", "Shift+H", "Ctrl+M"),
            Def(Act.MirrorZ, "MirrorZ", "Mirror along Z", GroupEdit,
                "Mirror the selection across its middle along Z", "Shift+V", "Shift+V", "Ctrl+Shift+M"),
            Def(Act.CopyInRow, "CopyInRow", "Copy in a row", GroupEdit,
                "A copy of the selection right next to it, to the camera's right. Again lays the next one.",
                "Ctrl+Shift+D", "Ctrl+Shift+D", "Ctrl+Alt+D"),
            Def(Act.Delete, "Delete", "Delete", GroupEdit, "Delete the selection", "Delete, Backspace", "Delete, Backspace", "X, Delete"),
            Def(Act.Undo, "Undo", "Undo", GroupEdit, "Undo", "Ctrl+Z", "Ctrl+Z", "Ctrl+Z"),
            Def(Act.Redo, "Redo", "Redo", GroupEdit, "Redo", "Ctrl+Shift+Z, Ctrl+Y", "Ctrl+Shift+Z, Ctrl+Y", "Ctrl+Shift+Z"),
            Def(Act.Save, "Save", "Save", GroupEdit, "Save", "Ctrl+S", "Ctrl+S", "Ctrl+S"),

            Def(Act.FlyForward, "FlyForward", "Fly forward", GroupFly,
                "Fly forward (where the camera looks)", "W", "W", "W"),
            Def(Act.FlyBack, "FlyBack", "Fly back", GroupFly, "Fly back", "S", "S", "S"),
            Def(Act.FlyLeft, "FlyLeft", "Fly left", GroupFly, "Fly left", "A", "A", "A"),
            Def(Act.FlyRight, "FlyRight", "Fly right", GroupFly, "Fly right", "D", "D", "D"),
            Def(Act.FlyUp, "FlyUp", "Fly up", GroupFly, "Fly up. Hold Shift to fly 3 times faster.", "Space", "Space", "Space"),
            Def(Act.FlyDown, "FlyDown", "Fly down", GroupFly, "Fly down", "LeftControl, RightControl",
                "LeftControl, RightControl", "LeftControl, RightControl"),
        };

        private static readonly Dictionary<string, KeyCode> Alias = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            { "Up", KeyCode.UpArrow }, { "Down", KeyCode.DownArrow }, { "Left", KeyCode.LeftArrow }, { "Right", KeyCode.RightArrow },
            { "Enter", KeyCode.Return }, { "Del", KeyCode.Delete }, { "PgUp", KeyCode.PageUp }, { "PgDn", KeyCode.PageDown },
            { "Esc", KeyCode.Escape }, { "/", KeyCode.Slash }, { "\\", KeyCode.Backslash }, { "?", KeyCode.Question },
            { ".", KeyCode.Period }, { "-", KeyCode.Minus }, { "=", KeyCode.Equals },
            { "`", KeyCode.BackQuote }, { "[", KeyCode.LeftBracket }, { "]", KeyCode.RightBracket }, { ";", KeyCode.Semicolon },
            { "'", KeyCode.Quote }, { "Num.", KeyCode.KeypadPeriod }, { "Ctrl", KeyCode.LeftControl },
        };

        private static readonly Dictionary<KeyCode, string> Names = new Dictionary<KeyCode, string>
        {
            { KeyCode.UpArrow, "Up" }, { KeyCode.DownArrow, "Down" }, { KeyCode.LeftArrow, "Left" }, { KeyCode.RightArrow, "Right" },
            { KeyCode.Return, "Enter" }, { KeyCode.Backslash, "\\" }, { KeyCode.Slash, "/" }, { KeyCode.Question, "?" },
            { KeyCode.Period, "." }, { KeyCode.Minus, "-" }, { KeyCode.Equals, "=" },
            { KeyCode.BackQuote, "`" }, { KeyCode.LeftBracket, "[" }, { KeyCode.RightBracket, "]" }, { KeyCode.Semicolon, ";" },
            { KeyCode.Quote, "'" }, { KeyCode.KeypadPeriod, "Num." },
        };

        /// <summary>Keys the Keys window can bind. A modifier on its own is not one of them.</summary>
        private static readonly KeyCode[] Bindable = BuildBindable();

        private static readonly Dictionary<Act, Chord[]> Cache = new Dictionary<Act, Chord[]>();
        private static ConfigEntry<string> _preset;

        /// <summary>Goes up whenever a binding changes, so a cached key list can notice.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<ActionDef> All => Table;

        /// <summary>The act the Keys window is waiting for a key for, or None.</summary>
        public static Act Capturing { get; private set; }

        public static string Preset => _preset != null ? _preset.Value : Presets[0];

        public static void Bind(ConfigFile config)
        {
            _preset = config.Bind(
                "Keys",
                "Preset",
                Presets[0],
                new ConfigDescription(
                    "Which set of keys the editor starts from: Tomrer (the editor's own), Figma or Blender. "
                    + "The settings below replace single actions.",
                    new AcceptableValueList<string>(Presets)));
            _preset.SettingChanged += (s, e) => Changed();

            foreach (var def in Table)
            {
                def.Entry = config.Bind(
                    "Keys",
                    def.Name,
                    "",
                    $"{def.Label}. Empty uses the preset's keys, none removes the key, else a list such as "
                    + $"\"{def.Tomrer}\". Names: letters, digits, F1 to F12, Tab, Space, Up, Down, Left, Right, PageUp, "
                    + "PageDown, Delete, Backspace, Home, End, with Ctrl+, Alt+ and Shift+ in front. The Keys window "
                    + "(H, then Change keys) edits these.");
                def.Entry.SettingChanged += (s, e) => Changed();
            }
        }

        private static void Changed()
        {
            Cache.Clear();
            Version++;
        }

        private static ActionDef Def(Act id, string name, string label, string group, string help,
            string tomrer, string figma, string blender)
        {
            return new ActionDef
            {
                Id = id, Name = name, Label = label, Group = group, Help = help,
                Tomrer = tomrer, Figma = figma, Blender = blender,
            };
        }

        public static ActionDef Of(Act act)
        {
            foreach (var def in Table)
            {
                if (def.Id == act)
                {
                    return def;
                }
            }

            return null;
        }

        /// <summary>The keys of an action now: its own setting when it has one, else the preset's.</summary>
        public static Chord[] Chords(Act act)
        {
            if (Cache.TryGetValue(act, out var cached))
            {
                return cached;
            }

            var def = Of(act);
            var text = def != null ? Text(def) : "";
            var chords = Parse(text);
            Cache[act] = chords;
            return chords;
        }

        /// <summary>What is written for the action: the setting, or the preset's text. "none" gives nothing.</summary>
        private static string Text(ActionDef def)
        {
            var own = def.Entry != null ? def.Entry.Value : "";
            return string.IsNullOrWhiteSpace(own) ? def.For(Preset) : own;
        }

        /// <summary>The keys as people read them: "Ctrl+Z, Ctrl+Y", or "none".</summary>
        public static string Describe(Act act)
        {
            var chords = Chords(act);
            return chords.Length == 0 ? "none" : string.Join(", ", chords.Select(c => c.ToString()));
        }

        /// <summary>True when the action has its own keys instead of the preset's.</summary>
        public static bool IsChanged(Act act)
        {
            var def = Of(act);
            return def != null && def.Entry != null && !string.IsNullOrWhiteSpace(def.Entry.Value);
        }

        // ---------- matching ----------

        /// <summary>
        /// The action for a key pressed with these modifiers, or None. Ctrl must be exactly as the chord
        /// says. Shift and Alt only have to be held when the chord asks for them, so holding Shift to
        /// turn snapping off still lets G start a move, and the chord that asks for the most wins
        /// (Shift+R over R, Ctrl+Shift+Z over Ctrl+Z).
        /// </summary>
        public static Act Match(KeyCode key, KeyMods mods)
        {
            var held = Normal(mods);
            var best = Act.None;
            var bestScore = -1;
            foreach (var def in Table)
            {
                if (def.Id >= Act.FlyForward)
                {
                    continue;   // flying is held, not pressed: see FlyKeys
                }

                foreach (var chord in Chords(def.Id))
                {
                    if (chord.Key != key || (chord.Mods & KeyMods.Ctrl) != (held & KeyMods.Ctrl))
                    {
                        continue;
                    }

                    if ((chord.Mods & held) != chord.Mods)
                    {
                        continue;
                    }

                    var score = Count(chord.Mods);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = def.Id;
                    }
                }
            }

            return best;
        }

        /// <summary>True when this key and these modifiers are one of the action's chords.</summary>
        public static bool Matches(Act act, KeyCode key, KeyMods mods)
        {
            return Match(key, mods) == act;
        }

        /// <summary>The keys that fly the camera in a direction (held, not pressed).</summary>
        public static IReadOnlyList<KeyCode> FlyKeys(Act act)
        {
            return Chords(act).Select(c => c.Key).ToArray();
        }

        /// <summary>True when the key is one of the fly keys.</summary>
        public static bool IsFlyKey(KeyCode key)
        {
            for (var act = Act.FlyForward; act <= Act.FlyDown; act++)
            {
                foreach (var chord in Chords(act))
                {
                    if (chord.Key == key)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Every key the editor listens for, so the dispatcher reads only those.</summary>
        public static IEnumerable<KeyCode> Keys()
        {
            var seen = new HashSet<KeyCode>();
            foreach (var def in Table)
            {
                foreach (var chord in Chords(def.Id))
                {
                    if (seen.Add(chord.Key))
                    {
                        yield return chord.Key;
                    }
                }
            }
        }

        /// <summary>The keys the Keys window offers to bind, for the dispatcher to read while it waits.</summary>
        public static IReadOnlyList<KeyCode> BindableKeys => Bindable;

        /// <summary>
        /// True when one of the action's chords was pressed this frame and is safe while a text box has
        /// the keyboard: it cannot be a letter that is being typed. Tab, the function keys and anything
        /// with Ctrl or Alt. The Quick add popup closes from its search box with it.
        /// </summary>
        public static bool TypedPressed(Act act)
        {
            foreach (var chord in Chords(act))
            {
                var safe = (chord.Mods & (KeyMods.Ctrl | KeyMods.Alt)) != 0 || chord.Key == KeyCode.Tab
                    || (chord.Key >= KeyCode.F1 && chord.Key <= KeyCode.F15);
                var wanted = Normal(chord.Mods) & (KeyMods.Ctrl | KeyMods.Alt);
                if (safe && ZInput.GetKeyDown(chord.Key, false) && (Normal(Bindings.Mods) & (KeyMods.Ctrl | KeyMods.Alt)) == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------- changing ----------

        public static void SetPreset(string preset)
        {
            if (_preset != null && Array.IndexOf(Presets, preset) >= 0 && _preset.Value != preset)
            {
                _preset.Value = preset;

                // A key the player set stays, so the new preset's action on that key gives it up.
                foreach (var def in Table)
                {
                    if (!IsChanged(def.Id))
                    {
                        continue;
                    }

                    foreach (var chord in Chords(def.Id))
                    {
                        Take(chord, def.Id);
                    }
                }
            }
        }

        /// <summary>The Keys window starts waiting for a key for this action.</summary>
        public static void StartCapture(Act act)
        {
            Capturing = act;
        }

        /// <summary>Esc while waiting. True when it was waiting.</summary>
        public static bool CancelCapture()
        {
            if (Capturing == Act.None)
            {
                return false;
            }

            Capturing = Act.None;
            return true;
        }

        /// <summary>
        /// Gives the waiting action this key. A chord another action already has is taken from that
        /// action (it is named in the result, for the message), so one key never does two things.
        /// </summary>
        public static string Capture(KeyCode key, KeyMods mods)
        {
            var act = Capturing;
            Capturing = Act.None;
            if (act == Act.None)
            {
                return null;
            }

            var chord = new Chord(key, Normal(mods) & (KeyMods.Ctrl | KeyMods.Alt | KeyMods.Shift));
            if (act >= Act.FlyForward)
            {
                chord.Mods = KeyMods.None;   // a held key has no modifiers
            }

            var taken = Take(chord, act);
            Write(act, new[] { chord });
            return taken;
        }

        /// <summary>No key for this action.</summary>
        public static void Clear(Act act)
        {
            Capturing = Act.None;
            Write(act, new Chord[0]);
        }

        /// <summary>Back to the preset's keys for this action.</summary>
        public static void Reset(Act act)
        {
            var def = Of(act);
            if (def != null && def.Entry != null)
            {
                def.Entry.Value = "";

                // The preset's key may be one another action took: it takes it back, as binding a key does.
                foreach (var chord in Chords(act))
                {
                    Take(chord, act);
                }
            }
        }

        public static void ResetAll()
        {
            foreach (var def in Table)
            {
                if (def.Entry != null)
                {
                    def.Entry.Value = "";
                }
            }

            Capturing = Act.None;
        }

        /// <summary>Takes a chord off every other action that has it. The label of the first, or null.</summary>
        private static string Take(Chord chord, Act except)
        {
            string name = null;
            foreach (var def in Table)
            {
                if (def.Id == except)
                {
                    continue;
                }

                var chords = Chords(def.Id);
                if (!chords.Any(c => c.Key == chord.Key && c.Mods == chord.Mods))
                {
                    continue;
                }

                name = name ?? def.Label;
                Write(def.Id, chords.Where(c => !(c.Key == chord.Key && c.Mods == chord.Mods)).ToArray());
            }

            return name;
        }

        private static void Write(Act act, Chord[] chords)
        {
            var def = Of(act);
            if (def == null || def.Entry == null)
            {
                return;
            }

            def.Entry.Value = chords.Length == 0 ? "none" : string.Join(", ", chords.Select(c => c.ToString()));
        }

        // ---------- text ----------

        /// <summary>"Ctrl+Shift+Z, Ctrl+Y" to chords. Words it does not know are skipped, "none" is empty.</summary>
        public static Chord[] Parse(string text)
        {
            var list = new List<Chord>();
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return list.ToArray();
            }

            foreach (var part in text.Split(','))
            {
                var word = part.Trim();
                if (word.Length == 0)
                {
                    continue;
                }

                // "Shift+/" and "Ctrl+\" end in a symbol, and "+" itself cannot be a key here.
                var pieces = word.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (pieces.Length == 0)
                {
                    continue;
                }

                var mods = KeyMods.None;
                var ok = true;
                for (var i = 0; i < pieces.Length - 1; i++)
                {
                    switch (pieces[i].ToLowerInvariant())
                    {
                        case "ctrl":
                        case "cmd":
                        case "control":
                            mods |= KeyMods.Ctrl;
                            break;
                        case "shift":
                            mods |= KeyMods.Shift;
                            break;
                        case "alt":
                            mods |= KeyMods.Alt;
                            break;
                        default:
                            ok = false;
                            break;
                    }
                }

                if (ok && TryKey(pieces[pieces.Length - 1], out var key))
                {
                    list.Add(new Chord(key, mods));
                }
            }

            return list.ToArray();
        }

        private static bool TryKey(string word, out KeyCode key)
        {
            if (Alias.TryGetValue(word, out key))
            {
                return true;
            }

            if (word.Length == 1 && word[0] >= '0' && word[0] <= '9')
            {
                key = KeyCode.Alpha0 + (word[0] - '0');
                return true;
            }

            return Enum.TryParse(word, true, out key) && key != KeyCode.None;
        }

        /// <summary>A key as people read it: "Z", "1", "Up", "Enter", "\".</summary>
        public static string KeyName(KeyCode key)
        {
            if (Names.TryGetValue(key, out var name))
            {
                return name;
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)(key - KeyCode.Alpha0)).ToString();
            }

            return key.ToString();
        }

        private static KeyMods Normal(KeyMods mods)
        {
            // Cmd is Ctrl for the table, so a Mac keeps every shortcut.
            if ((mods & KeyMods.Cmd) != 0)
            {
                mods = (mods & ~KeyMods.Cmd) | KeyMods.Ctrl;
            }

            return mods;
        }

        private static int Count(KeyMods mods)
        {
            var n = 0;
            for (var bit = 1; bit <= 8; bit <<= 1)
            {
                if (((int)mods & bit) != 0)
                {
                    n++;
                }
            }

            return n;
        }

        private static KeyCode[] BuildBindable()
        {
            var keys = new List<KeyCode>();
            for (var k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                keys.Add(k);
            }

            for (var k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
            {
                keys.Add(k);
            }

            for (var k = KeyCode.F1; k <= KeyCode.F12; k++)
            {
                if (k != KeyCode.F7 && k != KeyCode.F8)
                {
                    keys.Add(k);   // F7 and F8 are the editor and the capture
                }
            }

            keys.AddRange(new[]
            {
                KeyCode.Tab, KeyCode.Space, KeyCode.Backspace, KeyCode.Delete, KeyCode.Insert, KeyCode.Home, KeyCode.End,
                KeyCode.PageUp, KeyCode.PageDown, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Backslash, KeyCode.Slash, KeyCode.Minus, KeyCode.Equals, KeyCode.Comma, KeyCode.Period,
                KeyCode.BackQuote, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Semicolon, KeyCode.Quote,
                KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
                KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.KeypadPeriod,
                KeyCode.KeypadPlus, KeyCode.KeypadMinus, KeyCode.LeftControl,
            });
            return keys.ToArray();
        }
    }
}
