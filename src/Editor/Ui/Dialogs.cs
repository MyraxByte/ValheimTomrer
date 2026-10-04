using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Blueprints;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Input;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's windows on top of the window: the blueprint list, save as, help and the
    /// questions (unsaved changes, delete a file). One at a time, over a dark backdrop that
    /// swallows clicks.
    ///
    /// While one is up the editor's keys are silent (<see cref="Bindings.Press"/> reads
    /// <see cref="IsOpen"/>) and Esc closes it instead of the editor.
    /// </summary>
    internal static class Dialogs
    {
        /// <summary>A blueprint name has to look like this, and must not hold "..".</summary>
        public const string NamePattern = "^[A-Za-z0-9_][A-Za-z0-9_ .-]*$";

        private const float Head = 40f;
        private const float FootHeight = 44f;
        private const float Pad = 14f;
        private const float RowHeight = 28f;

        /// <summary>Where the name stops and the file and the date start.</summary>
        private const float NameSplit = 0.42f;

        /// <summary>The Delete button at the end of each of the player's own rows.</summary>
        private const float DeleteWidth = 84f;

        private const float DeleteGap = 6f;

        private static readonly Regex Name = new Regex(NamePattern);

        private static RectTransform _host;
        private static RectTransform _root;
        private static RectTransform _modal;
        private static RectTransform _body;
        private static RectTransform _buttons;
        private static TextMeshProUGUI _title;
        private static int _generation = -1;

        private static readonly List<BlueprintEntry> Rows = new List<BlueprintEntry>();
        private static readonly List<TextMeshProUGUI> RowLabels = new List<TextMeshProUGUI>();
        private static readonly List<TextMeshProUGUI> RowDetails = new List<TextMeshProUGUI>();
        private static readonly List<Selectable> RowButtons = new List<Selectable>();

        /// <summary>One per row, null for a kit: kits live in the DLL.</summary>
        private static readonly List<Button> DeleteButtons = new List<Button>();

        private static Action _confirmRun;

        /// <summary>What cancelling the dialog goes back to, or null to just close it.</summary>
        private static Action _back;
        private static Func<string, bool, bool> _saveCommit;
        private static string _saveTitle = "Save as";
        private static bool _insertMode;
        private static TextMeshProUGUI _note;
        private static TextMeshProUGUI _fileLine;
        private static Button _submit;

        /// <summary>"open", "saveAs", "help", "confirm", or "" when none is up.</summary>
        public static string Kind { get; private set; } = "";

        public static bool IsOpen => Kind.Length > 0;

        /// <summary>The name box of the save-as dialog.</summary>
        public static TMP_InputField NameField { get; private set; }

        /// <summary>The panel itself, without the backdrop: what the controller walk covers.</summary>
        public static RectTransform Modal => _modal;

        /// <summary>The widget the controller walk lands on when the dialog opens.</summary>
        public static Selectable FocusStart { get; private set; }

        /// <summary>The line under a field: why the name is refused, or that it exists already.</summary>
        public static string NoteText => _note != null && _note.gameObject.activeSelf ? _note.text : "";

        public static string TitleText => _title != null ? _title.text : "";

        /// <summary>Rows of the open dialog, the mod's own blueprints first.</summary>
        public static int RowCount => Rows.Count;

        /// <summary>Everything one row says: its name, then the file and the date.</summary>
        public static string RowText(int index)
        {
            if (index < 0 || index >= RowLabels.Count)
            {
                return "";
            }

            return RowLabels[index].text + "   " + RowDetails[index].text;
        }

        public static BlueprintEntry Row(int index)
        {
            return index >= 0 && index < Rows.Count ? Rows[index] : null;
        }

        /// <summary>The row itself, the widget a click or cross opens.</summary>
        public static Selectable RowWidget(int index)
        {
            return index >= 0 && index < RowButtons.Count ? RowButtons[index] : null;
        }

        /// <summary>The row's Delete button, or null for a kit.</summary>
        public static Button DeleteButton(int index)
        {
            return index >= 0 && index < DeleteButtons.Count ? DeleteButtons[index] : null;
        }

        /// <summary>What a click on a row does. The test calls it without a mouse.</summary>
        public static void ClickRow(int index)
        {
            var entry = Row(index);
            if (entry == null)
            {
                return;
            }

            if (entry.Error != null)
            {
                Toasts.Error(entry.Error);
                return;
            }

            if (_insertMode)
            {
                EditorCommands.InsertEntry(entry);
                return;
            }

            EditorCommands.OpenEntry(entry);
        }

        /// <summary>
        /// What the Delete button on a row does: asks first, on Cancel. Either answer comes back
        /// to the list, on the same place.
        /// </summary>
        public static void ClickDelete(int index)
        {
            var entry = Row(index);
            if (entry == null || entry.IsKit || string.IsNullOrEmpty(entry.Path))
            {
                return;
            }

            var file = Path.GetFileName(entry.Path);
            var document = EditorState.Document;
            var open = document != null && !string.IsNullOrEmpty(document.SourcePath)
                && string.Equals(document.SourcePath, entry.Path, StringComparison.OrdinalIgnoreCase);
            var text = $"{NameOf(entry)} ({file}) is deleted from your blueprint folder. This cannot be undone."
                + (open ? " It stays open in the editor, as not saved." : "");
            Confirm("Delete the blueprint?", text, "Delete",
                () =>
                {
                    EditorCommands.DeleteEntry(entry);
                    Open(index);
                },
                () => Open(index),
                true);
        }

        public static void Ensure(RectTransform host)
        {
            if (host == null || (_host == host && _root != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _host = host;
            _generation = UiTheme.Generation;

            // The old dialog's widgets are gone with the old window; what points at them must go too, or the next
            // dialog adds to stale lists and the pad starts on a destroyed button.
            Keymap.CancelCapture();
            ForgetWidgets();
            Build(host);
        }

        /// <summary>Enter answers the question that is up. Esc goes through Bindings.Cancel.</summary>
        public static void Tick()
        {
            if (!IsOpen || ModUi.JustTyping || Keymap.Capturing != Act.None)
            {
                return;
            }

            // A selected button, or a widget the walk has, gets cross and Enter itself. Either
            // way this has to stand back, or one press fires twice.
            if (!ModUi.HasSelection && !FocusNav.InDialog && EditorInput.Confirm)
            {
                Submit();
            }
        }

        /// <summary>Presses the dialog's own button, the one Enter presses.</summary>
        public static void Submit()
        {
            if (_submit != null && _submit.interactable)
            {
                _submit.onClick.Invoke();
            }
        }

        /// <summary>
        /// Cancels whatever is up: Esc, circle, the X, a click beside it, a Cancel button. A
        /// question asked from the blueprint list goes back to that list. True when there was
        /// something.
        /// </summary>
        public static bool Dismiss()
        {
            var back = _back;
            if (!Close())
            {
                return false;
            }

            back?.Invoke();
            return true;
        }

        /// <summary>Shuts whatever is up. True when there was something.</summary>
        public static bool Close()
        {
            if (!IsOpen)
            {
                return false;
            }

            Keymap.CancelCapture();
            ForgetWidgets();
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }

            // The walk goes back to the panel it came from, or off if it was off.
            FocusNav.LeaveDialog();
            return true;
        }

        /// <summary>Lets go of everything the open dialog's widgets were kept in.</summary>
        private static void ForgetWidgets()
        {
            KeyCells.Clear();
            PresetLabels.Clear();
            CommandsShown.Clear();
            _commandRows = null;
            Kind = "";
            _confirmRun = null;
            _back = null;
            _saveCommit = null;
            _insertMode = false;
            NameField = null;
            FocusStart = null;
            _note = null;
            _fileLine = null;
            _submit = null;
            Rows.Clear();
            RowLabels.Clear();
            RowDetails.Clear();
            RowButtons.Clear();
            DeleteButtons.Clear();
        }

        // ---------- the dialogs ----------

        /// <summary>
        /// The blueprints that come with the mod first, with no heading over them, then the
        /// player's own files under one, each with a Delete button. <paramref name="deleteAt"/>
        /// puts the walk on the Delete button of that row (or the nearest one above), for the
        /// way back from the delete question.
        /// </summary>
        public static void Open(int deleteAt = -1, bool insert = false)
        {
            if (!Begin("open", insert ? "Insert a blueprint" : "Blueprints", 780f, 580f))
            {
                return;
            }

            _insertMode = insert;

            var scroll = UiBuild.Scroll("Files", _body, 2f);
            UiBuild.Stretch((RectTransform)scroll.transform);

            AddRows(scroll.content, DocumentStore.ListKits(), false);
            Gap(scroll.content, 12f);
            Heading(scroll.content, "Your blueprints");
            var files = DocumentStore.ListUserFiles();
            if (files.Count == 0)
            {
                Dim(scroll.content, "None yet. Save as writes one here.");
            }

            AddRows(scroll.content, files, !insert);
            Foot("Close", () => Close(), null, null);

            // The rows need their places now, or the walk cannot scroll a row far down into sight.
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            // The walk starts on the first blueprint, so a controller can pick one straight away.
            Selectable start = null;
            for (var i = Mathf.Min(deleteAt, DeleteButtons.Count - 1); i >= 0 && start == null; i--)
            {
                start = DeleteButtons[i];
            }

            Start(start != null ? start : RowButtons.Count > 0 ? RowButtons[0] : _submit);
        }

        /// <summary>The name a blueprint is saved under. It becomes #Name: and, slugged, the file name.</summary>
        public static void SaveAs(string initial)
        {
            SaveAs(initial, null, "Save as");
        }

        /// <summary>
        /// The same box with another way to save: <paramref name="commit"/> gets the name and whether to overwrite.
        /// The selection is saved as a blueprint of its own this way.
        /// </summary>
        public static void SaveAs(string initial, Func<string, bool, bool> commit, string title)
        {
            if (!Begin("saveAs", title, 580f, 320f))
            {
                return;
            }

            _saveCommit = commit;
            _saveTitle = title;

            var label = UiBuild.Label("Label", _body, "Blueprint name", 16f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
            Line(label.rectTransform, 0f, 20f);

            NameField = UiBuild.InputField("Name", _body, "Name", 34f);
            Line(NameField.GetComponent<RectTransform>(), 24f, 34f);
            NameField.characterLimit = 60;
            NameField.text = initial ?? "";
            NameField.onValueChanged.AddListener(_ => CheckName());
            NameField.onSubmit.AddListener(_ => SubmitSaveAs());

            _fileLine = UiBuild.Label("File", _body, "", 14f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
            Line(_fileLine.rectTransform, 64f, 20f);

            _note = UiBuild.Label("Note", _body, "", 14f, TextAlignmentOptions.TopLeft, UiTheme.Accent);
            _note.enableWordWrapping = true;
            Line(_note.rectTransform, 88f, 40f);

            var hint = UiBuild.Label("Hint", _body,
                "Letters, digits, spaces, dots, dashes and underscores. Files go into the mod's blueprint folder.",
                14f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
            hint.enableWordWrapping = true;
            Line(hint.rectTransform, 132f, 40f);

            Foot("Cancel", () => Close(), "Save", SubmitSaveAs);
            CheckName();
            Start(NameField);

            // The keyboard types straight away. The pad gets the ring on the box instead, so the
            // D-pad reaches Save at once: a box that is typing holds the walk, and the pad has no
            // keys to type with (cross on the box starts typing when a keyboard is at hand).
            if (!EditorInput.PadInUse)
            {
                NameField.ActivateInputField();
            }
        }

        /// <summary>Everything the mouse, the keys and the controller do.</summary>
        public static void Help()
        {
            if (!Begin("help", "Mouse, keys and controller", 1000f, 860f))
            {
                return;
            }

            var scroll = UiBuild.Scroll("Help", _body, 2f);
            UiBuild.Stretch((RectTransform)scroll.transform);

            Heading(scroll.content, "Mouse and keyboard");
            foreach (var row in Bindings.Keys)
            {
                KeyRow(scroll.content, row);
            }

            Dim(scroll.content, "On a Mac, Cmd works everywhere Ctrl does.");
            Heading(scroll.content, "Controller");
            foreach (var row in Bindings.Pad)
            {
                KeyRow(scroll.content, row);
            }

            Dim(scroll.content,
                "The pad reaches the panels too. L3 with an empty hand leaves the view and opens the walk, "
                + "and circle is the way back to the view. The D-pad or the left stick moves to the "
                + "button above, below, left or right, and on into the next panel at an edge. R1 goes on "
                + "through left, top, right and wraps, L1 goes back the same way, cross presses. F6 does "
                + "the same from the keyboard. The piece list and the problem list stay "
                + "mouse-only: pick pieces with the cross menu instead.");

            Dim(scroll.content,
                "A window like this one takes the walk on its own: the D-pad moves through what it holds, "
                + "cross presses, and circle closes it. Save as opened from the pad puts the ring on the "
                + "name box without typing, so down and cross save under the name shown. In a name box "
                + "that is typing, circle hands the keyboard back and the D-pad walks on out of it. In "
                + "Blueprints, right from one of your own blueprints goes to its Delete button, which "
                + "asks first.");

            Dim(scroll.content,
                "Placing works like the game: the piece touches the surface you aim at, then snaps to the "
                + "closest snap point within 0.5 m. When that finds nothing the editor also slides it to the "
                + "closest spot that touches the point under the cursor. The red arrow marks the front: it "
                + "faces the player when the mod builds the blueprint.");
            Foot("Close", () => Close(), "Change keys", () => Controls());
            Start(_submit);
        }

        private static readonly Dictionary<Act, TextMeshProUGUI> KeyCells = new Dictionary<Act, TextMeshProUGUI>();
        private static readonly List<TextMeshProUGUI> PresetLabels = new List<TextMeshProUGUI>();

        /// <summary>
        /// The Keys window: a preset to start from, and every action with its key. A click on a row waits
        /// for the next key (<see cref="Keymap.StartCapture"/>), a right click goes back to the preset's key.
        /// The pad can open it and pick a preset; binding a key needs the keyboard.
        /// </summary>
        public static void Controls()
        {
            if (!Begin("keys", "Keys", 860f, 780f))
            {
                return;
            }

            KeyCells.Clear();
            PresetLabels.Clear();
            var intro = UiBuild.Label("Intro", _body,
                "Pick a preset, or click a key to change it and press the new one. Right click goes back to the "
                + "preset's key. Esc cancels, Backspace takes the key away. A key that another action has moves to "
                + "this one. The controller keeps the game's own layout.",
                14f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
            intro.enableWordWrapping = true;
            Line(intro.rectTransform, 0f, 58f);

            var presets = UiBuild.Row("Presets", _body, 6f);
            Line(presets, 62f, 34f);
            var walk = new List<Selectable>();
            foreach (var preset in Keymap.Presets)
            {
                var name = preset;
                var button = UiBuild.Button("Preset " + name, presets, name, () =>
                {
                    Keymap.SetPreset(name);
                    RefreshKeys();
                }, 34f);
                Width(button, 130f);
                PresetLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>());
                walk.Add(button);
            }

            UiBuild.LinkRow(walk);

            var scroll = UiBuild.Scroll("Keys", _body, 2f);
            UiBuild.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 104f);
            var group = "";
            foreach (var def in Keymap.All)
            {
                if (def.Group != group)
                {
                    group = def.Group;
                    Heading(scroll.content, group);
                }

                KeyBinderRow(scroll.content, def);
            }

            Foot("Reset all", () => Confirm(
                "Reset every key?",
                "Every key you changed goes back to the " + Keymap.Preset + " preset.",
                "Reset",
                () =>
                {
                    Keymap.ResetAll();
                    Toasts.Info("Keys: back to the " + Keymap.Preset + " preset.");
                    Controls();
                },
                () => Controls(),
                true), "Close", () => Close());
            Start(_submit);
            RefreshKeys();
        }

        private sealed class Command
        {
            public string Label;
            public string Keys;
            public Action Run;
        }

        private static readonly List<Command> CommandsShown = new List<Command>();
        private static RectTransform _commandRows;

        /// <summary>The command search: every action and top bar verb by name, with its keys. Enter runs the first.</summary>
        public static void Commands()
        {
            if (!Begin("commands", "Commands", 640f, 620f))
            {
                return;
            }

            var search = UiBuild.InputField("Search", _body, "Type to find a command", 34f);
            Line((RectTransform)search.transform, 0f, 34f);
            var scroll = UiBuild.Scroll("Commands", _body, 2f);
            UiBuild.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 42f);
            _commandRows = scroll.content;
            search.onValueChanged.AddListener(FilterCommands);
            search.onSubmit.AddListener(_ => RunFirstCommand());
            FilterCommands("");

            Foot("Close", () => Close(), "Run", RunFirstCommand);
            Start(_submit);

            // The keyboard types straight away; the pad walks the list instead.
            if (!EditorInput.PadInUse)
            {
                search.ActivateInputField();
            }
        }

        /// <summary>Every command the search knows, in the order it lists them.</summary>
        private static List<Command> AllCommands()
        {
            var list = new List<Command>
            {
                new Command { Label = "New blueprint", Keys = "", Run = EditorCommands.NewBlueprint },
                new Command { Label = "Open a blueprint", Keys = "", Run = EditorCommands.OpenDialog },
                new Command
                {
                    Label = "Save as", Keys = "",
                    Run = () => SaveAs(EditorState.Document != null ? EditorState.Document.Name : "New blueprint"),
                },
                new Command { Label = "Build this in the world", Keys = "", Run = () => EditorCommands.BuildThis() },
                new Command { Label = "Center origin", Keys = "", Run = EditorCommands.CenterOrigin },
                new Command { Label = "Pieces as boxes", Keys = "", Run = EditorCommands.ToggleBoxes },
                new Command { Label = "Snap dots", Keys = "", Run = EditorCommands.ToggleSnapDots },
                new Command { Label = "Keys window", Keys = "", Run = Controls },
            };

            foreach (var def in Keymap.All)
            {
                if (def.Id >= Act.FlyForward || def.Id == Act.Commands || def.Id == Act.Walk || def.Id == Act.WalkBack)
                {
                    continue;
                }

                var act = def.Id;
                list.Add(new Command { Label = def.Label, Keys = Keymap.Describe(act), Run = () => Bindings.Run(act) });
            }

            return list;
        }

        private static void FilterCommands(string text)
        {
            if (_commandRows == null)
            {
                return;
            }

            for (var i = _commandRows.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(_commandRows.GetChild(i).gameObject);
            }

            CommandsShown.Clear();
            var words = (text ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var command in AllCommands())
            {
                var label = command.Label.ToLowerInvariant();
                var all = true;
                foreach (var word in words)
                {
                    if (label.IndexOf(word, StringComparison.Ordinal) < 0)
                    {
                        all = false;
                        break;
                    }
                }

                if (!all)
                {
                    continue;
                }

                CommandsShown.Add(command);
                var chosen = command;
                var background = UiBuild.Panel("Command", _commandRows, null, UiTheme.Slot);
                background.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => RunCommand(chosen));
                Cell(background.transform, "Name", command.Label, 15f, TextAlignmentOptions.MidlineLeft,
                    CommandsShown.Count == 1 ? UiTheme.Accent : UiTheme.Text, 0f, 0.62f, 10f, 6f);
                Cell(background.transform, "Keys", command.Keys, 14f, TextAlignmentOptions.MidlineRight,
                    UiTheme.TextDim, 0.62f, 1f, 6f, 10f);
            }

            // Nothing matches: say so, and Enter and Run do nothing (they would otherwise do nothing silently).
            if (CommandsShown.Count == 0)
            {
                var none = UiBuild.Label("None", _commandRows, $"No command matches \"{text}\".", 15f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            }
        }

        private static void RunFirstCommand()
        {
            if (CommandsShown.Count > 0)
            {
                RunCommand(CommandsShown[0]);
            }
        }

        /// <summary>Closes the search first: many commands open a window of their own.</summary>
        private static void RunCommand(Command command)
        {
            Close();
            command.Run?.Invoke();
        }

        /// <summary>Shows the keys as they are now, after a binding or a preset changed. Does nothing unless the Keys window is up.</summary>
        public static void RefreshKeys()
        {
            if (Kind != "keys")
            {
                return;
            }

            foreach (var pair in KeyCells)
            {
                var waiting = Keymap.Capturing == pair.Key;
                pair.Value.text = waiting ? "press a key…" : Keymap.Describe(pair.Key);
                pair.Value.color = waiting ? UiTheme.Warn : Keymap.IsChanged(pair.Key) ? UiTheme.Accent : UiTheme.Text;
            }

            for (var i = 0; i < PresetLabels.Count; i++)
            {
                PresetLabels[i].color = Keymap.Presets[i] == Keymap.Preset ? UiTheme.Accent : UiTheme.Text;
            }
        }

        /// <summary>One action of the Keys window: its name on the left, its keys on the right.</summary>
        private static void KeyBinderRow(Transform parent, ActionDef def)
        {
            var background = UiBuild.Panel("Row " + def.Name, parent, null, UiTheme.Slot);
            background.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            var act = def.Id;
            button.onClick.AddListener(() =>
            {
                Keymap.StartCapture(act);
                RefreshKeys();
            });
            background.gameObject.AddComponent<KeyRowEvents>().Act = act;

            Cell(background.transform, "Name", def.Label, 15f, TextAlignmentOptions.MidlineLeft, UiTheme.Text, 0f, 0.55f, 10f, 6f);
            KeyCells[act] = Cell(background.transform, "Keys", Keymap.Describe(act), 15f,
                TextAlignmentOptions.MidlineRight, UiTheme.Text, 0.55f, 1f, 6f, 10f);
        }

        /// <summary>A right click on a row of the Keys window gives the action its preset key back.</summary>
        private sealed class KeyRowEvents : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            public Act Act;

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (eventData.button != UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                {
                    return;
                }

                Keymap.CancelCapture();
                Keymap.Reset(Act);
                RefreshKeys();
            }
        }

        /// <summary>
        /// A yes or no question. The answer runs after the dialog is gone. <paramref name="back"/>
        /// runs on every way of cancelling it. A <paramref name="risky"/> one (it cannot be
        /// undone) starts the walk on Cancel, so a second press does no harm.
        /// </summary>
        public static void Confirm(string title, string text, string ok, Action run, Action back = null, bool risky = false)
        {
            // A question asked from the Blueprints list or from Save as goes back to it when cancelled.
            var before = Kind;
            var typed = NameField != null ? NameField.text : null;
            var commitWas = _saveCommit;
            var titleWas = _saveTitle;
            var insertWas = _insertMode;
            if (!Begin("confirm", title, 560f, 240f))
            {
                return;
            }

            if (back == null && before == "open")
            {
                back = () => Open(-1, insertWas);
            }
            else if (back == null && before == "saveAs" && typed != null)
            {
                back = () => SaveAs(typed, commitWas, titleWas);
            }

            _confirmRun = run;
            _back = back;
            var label = UiBuild.Label("Text", _body, text, 16f, TextAlignmentOptions.TopLeft);
            label.enableWordWrapping = true;
            UiBuild.Stretch(label.rectTransform);
            var cancel = Foot("Cancel", () => Dismiss(), ok, Answer);
            Start(risky ? cancel : _submit);
        }

        // ---------- building ----------

        /// <summary>Clears whatever was up and starts a new one. False when the UI is not built.</summary>
        private static bool Begin(string kind, string title, float width, float height)
        {
            if (_root == null)
            {
                return false;
            }

            Close();
            Kind = kind;
            FocusStart = null;
            _title.text = title;

            for (var i = _body.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(_body.GetChild(i).gameObject);
            }

            for (var i = _buttons.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(_buttons.GetChild(i).gameObject);
            }

            var space = _host.rect;
            _modal.sizeDelta = new Vector2(
                Mathf.Min(width, Mathf.Max(360f, space.width - 120f)),
                Mathf.Min(height, Mathf.Max(220f, space.height - 120f)));
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();   // over the piece menu, which also sets itself last
            return true;
        }

        /// <summary>
        /// The buttons along the bottom. The second one is the one Enter presses. Hands back the
        /// first one.
        /// </summary>
        private static Button Foot(string left, Action onLeft, string right, Action onRight)
        {
            var cancel = UiBuild.Button("Left", _buttons, left, () => onLeft());
            Width(cancel, 120f);
            if (right == null)
            {
                _submit = cancel;
                return cancel;
            }

            _submit = UiBuild.Button("Right", _buttons, right, () => onRight());
            Width(_submit, 140f);

            // The pad walks the two buttons; cross presses the selected one (Tick stands back).
            UiBuild.LinkRow(new List<Selectable> { cancel, _submit });
            return cancel;
        }

        /// <summary>
        /// Says where the controller walk starts, and takes it into the dialog. Called last by
        /// every dialog, once everything it holds exists.
        /// </summary>
        private static void Start(Selectable widget)
        {
            FocusStart = widget;
            FocusNav.EnterDialog();
        }

        private static void Width(Button button, float width)
        {
            var element = button.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
        }

        private static void Answer()
        {
            var run = _confirmRun;
            Close();
            run?.Invoke();
        }

        /// <summary>A label pinned a set distance below the body's top edge.</summary>
        private static void Line(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        /// <summary>Air between two lists.</summary>
        private static void Gap(Transform parent, float height)
        {
            UiBuild.Rect("Gap", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static void Heading(Transform parent, string text)
        {
            if (parent == null)
            {
                return;
            }

            var label = UiBuild.Label("Heading", parent, text, 17f, TextAlignmentOptions.Left, UiTheme.Accent);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        }

        private static void Dim(Transform parent, string text)
        {
            if (parent == null || text == null)
            {
                return;
            }

            var label = UiBuild.Label("Note", parent, text, 14f, TextAlignmentOptions.TopLeft, UiTheme.TextDim);
            label.enableWordWrapping = true;
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = Mathf.Max(20f, label.GetPreferredValues(text, Mathf.Max(300f, _modal.rect.width - 80f), 0f).y + 4f);
        }

        /// <summary>One line of the help table: the keys on the left, what they do on the right.</summary>
        private static void KeyRow(Transform parent, HelpRow row)
        {
            // A row is as tall as its longest text: the words wrap in the room they get, and the layout reads
            // that room, so a long line never runs into the next row.
            var host = UiBuild.Rect("Row", parent);
            var line = host.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 8f;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            host.gameObject.AddComponent<LayoutElement>().minHeight = 22f;

            var keys = UiBuild.Label("Keys", host, row.Keys, 14f, TextAlignmentOptions.TopLeft, UiTheme.Accent);
            keys.enableWordWrapping = true;
            var keysSize = keys.gameObject.AddComponent<LayoutElement>();
            keysSize.minWidth = keysSize.preferredWidth = 250f;

            var what = UiBuild.Label("What", host, row.What, 14f, TextAlignmentOptions.TopLeft);
            what.enableWordWrapping = true;
            what.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        /// <summary>
        /// One clickable row: the blueprint's name on the left, and on the right where it is and
        /// when it changed. A <paramref name="deletable"/> row has a Delete button after it, on
        /// the same line, so the walk reaches it with a step to the right.
        /// </summary>
        private static void AddRows(Transform parent, List<BlueprintEntry> entries, bool deletable)
        {
            foreach (var entry in entries)
            {
                var index = Rows.Count;
                Rows.Add(entry);

                // item_background is a pale sprite, so the chip is tinted dark and the text on it
                // stays white. A colour transition would tint it a second time on hover and undo
                // that, so the row has none: the walk's ring is what marks the current one.
                var line = deletable ? UiBuild.Rect("Line", parent) : null;
                var background = UiBuild.Panel("Row", deletable ? line : parent, null, UiTheme.Slot);
                (deletable ? line : background.rectTransform).gameObject.AddComponent<LayoutElement>().preferredHeight =
                    RowHeight;
                DeleteButtons.Add(deletable ? DeleteAfter(line, background.rectTransform, index) : null);

                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ClickRow(index));
                RowButtons.Add(button);

                var colour = entry.Error != null ? UiTheme.Warn : UiTheme.Text;
                var name = Cell(background.transform, "Name", NameOf(entry), 15f,
                    TextAlignmentOptions.MidlineLeft, colour, 0f, NameSplit, 10f, 6f);
                RowLabels.Add(name);

                var detail = Cell(background.transform, "Detail", RowLine(entry), 12f,
                    TextAlignmentOptions.MidlineRight, entry.Error != null ? UiTheme.Warn : UiTheme.TextDim,
                    NameSplit, 1f, 6f, 10f);
                RowDetails.Add(detail);
            }
        }

        /// <summary>The row gives up its right end to a Delete button of the game's button style.</summary>
        private static Button DeleteAfter(RectTransform line, RectTransform row, int index)
        {
            UiBuild.Stretch(row, 0f, 0f, DeleteWidth + DeleteGap, 0f);
            var delete = UiBuild.Button("Delete", line, "Delete", () => ClickDelete(index), RowHeight);
            delete.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15f;
            var rect = (RectTransform)delete.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(DeleteWidth, 0f);
            return delete;
        }

        /// <summary>One column of a row, pinned between two fractions of its width.</summary>
        private static TextMeshProUGUI Cell(
            Transform parent,
            string name,
            string text,
            float size,
            TextAlignmentOptions align,
            Color colour,
            float from,
            float to,
            float left,
            float right)
        {
            var label = UiBuild.Label(name, parent, text, size, align, colour);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.anchorMin = new Vector2(from, 0f);
            label.rectTransform.anchorMax = new Vector2(to, 1f);
            label.rectTransform.offsetMin = new Vector2(left, 0f);
            label.rectTransform.offsetMax = new Vector2(-right, 0f);
            return label;
        }

        private static string NameOf(BlueprintEntry entry)
        {
            return string.IsNullOrEmpty(entry.Name) ? "(no name)" : entry.Name;
        }

        /// <summary>Where it is and when it changed, in one line on the right.</summary>
        private static string RowLine(BlueprintEntry entry)
        {
            var parts = entry.IsKit
                ? "in the mod"
                : Relative(entry.Path) + "   " + entry.Modified.ToString("d MMM yyyy HH:mm");
            if (entry.Error != null)
            {
                return parts + "   " + entry.Error;
            }

            return parts + $"   {entry.Pieces} pieces";
        }

        private static string Relative(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            var folder = BlueprintLibrary.UserFolder;
            return path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(folder.Length).TrimStart(Path.DirectorySeparatorChar)
                : path;
        }

        // ---------- save as ----------

        /// <summary>Reads the name box and says what would happen. Also switches Save on and off.</summary>
        private static void CheckName()
        {
            if (NameField == null)
            {
                return;
            }

            var wanted = NameField.text ?? "";
            var valid = wanted.Length > 0 && Name.IsMatch(wanted) && !wanted.Contains("..");
            var file = BlueprintFormat.FileNameFor(wanted);
            _fileLine.text = valid ? "File: " + file : "";

            if (!valid)
            {
                _note.text = wanted.Length == 0
                    ? "Give the blueprint a name."
                    : "Use letters, digits, spaces, dots, dashes or underscores.";
                _note.color = UiTheme.Warn;
            }
            else if (File.Exists(Path.Combine(BlueprintLibrary.UserFolder, file)))
            {
                _note.text = file + " exists. You will be asked before it is replaced.";
                _note.color = UiTheme.Accent;
            }
            else
            {
                _note.text = "";
            }

            _note.gameObject.SetActive(_note.text.Length > 0);
            if (_submit != null)
            {
                _submit.interactable = valid;
            }
        }

        private static void SubmitSaveAs()
        {
            if (NameField == null || _submit == null || !_submit.interactable)
            {
                return;
            }

            var wanted = NameField.text ?? "";
            var file = BlueprintFormat.FileNameFor(wanted);
            var target = Path.Combine(BlueprintLibrary.UserFolder, file);
            var document = EditorState.Document;
            var commit = _saveCommit ?? EditorCommands.SaveAs;
            var same = _saveCommit == null && document != null && !string.IsNullOrEmpty(document.SourcePath)
                && string.Equals(document.SourcePath, target, StringComparison.OrdinalIgnoreCase);
            if (!same && File.Exists(target))
            {
                Confirm("Replace the file?", $"{file} is already there. Saving writes over it.", "Replace",
                    () => commit(wanted, true));
                return;
            }

            commit(wanted, true);
        }

        // ---------- widgets ----------

        private static void Build(RectTransform host)
        {
            _root = UiBuild.Rect("Dialogs", host);
            UiBuild.Stretch(_root);
            _root.SetAsLastSibling();

            var backdrop = UiBuild.Panel("Backdrop", _root, null, new Color(0f, 0f, 0f, 0.55f));
            UiBuild.Stretch(backdrop.rectTransform);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => Dismiss());

            var panel = UiBuild.Card("Modal", _root);
            _modal = panel.rectTransform;
            _modal.anchorMin = _modal.anchorMax = new Vector2(0.5f, 0.5f);
            _modal.pivot = new Vector2(0.5f, 0.5f);
            _modal.anchoredPosition = Vector2.zero;
            _modal.sizeDelta = new Vector2(700f, 500f);

            _title = UiBuild.Label("Title", _modal, "", 20f, TextAlignmentOptions.Left, UiTheme.Text);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.offsetMin = new Vector2(Pad + 8f, -Head - 6f);
            _title.rectTransform.offsetMax = new Vector2(-Pad - 40f, -8f);

            var close = UiBuild.Button("Close", _modal, "X", () => Dismiss(), 26f);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-Pad, -10f);
            closeRect.sizeDelta = new Vector2(30f, 26f);

            _body = UiBuild.Rect("Body", _modal);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(Pad + 8f, Pad + FootHeight);
            _body.offsetMax = new Vector2(-Pad - 8f, -Head - 12f);

            _buttons = UiBuild.Row("Buttons", _modal, 8f);
            _buttons.anchorMin = new Vector2(1f, 0f);
            _buttons.anchorMax = new Vector2(1f, 0f);
            _buttons.pivot = new Vector2(1f, 0f);
            _buttons.anchoredPosition = new Vector2(-Pad - 8f, Pad);
            _buttons.sizeDelta = new Vector2(300f, 34f);
            _buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;

            _root.gameObject.SetActive(false);
        }
    }
}
