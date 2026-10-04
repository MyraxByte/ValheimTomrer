using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Input;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The three islands along the top of the screen. Left: the menu button (one menu instead of a
    /// desktop app's File, Edit, View) and the blueprint's name with its state. Middle: the modes,
    /// Select, Add, Move and Copy, the one that is on lit. Right: undo, redo, the command search, the
    /// theme, the help, and the one primary button, Build in world. Each island is as wide as what is
    /// in it; between them the view shows.
    /// </summary>
    internal static class Header
    {
        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;

        private static TextMeshProUGUI _name;
        private static Image _dirty;
        private static TextMeshProUGUI _state;
        private static Image _stateBack;
        private static TextMeshProUGUI _busy;
        private static TabStrip _modes;
        private static Button _undo;
        private static Button _redo;
        private static Button _build;
        private static Button _menuButton;
        private static RectTransform _menu;
        private static RectTransform _menuCard;
        private static ScrollRect _menuScroll;
        private static readonly List<RectTransform> _islands = new List<RectTransform>();

        /// <summary>Every button in the bar, in the order the pad walks them.</summary>
        private static readonly List<Selectable> Walk = new List<Selectable>();

        private static readonly string[] ModeNames = { "Select", "Add", "Move", "Copy" };

        /// <summary>The blueprint line: the name, a dot when it has changes. For the tests.</summary>
        public static string FileText => _name != null ? _name.text + (_dirty != null && _dirty.enabled ? " •" : "") : "";

        /// <summary>The state chip: read only, not saved yet, or the file name.</summary>
        public static string StateText => _state != null ? _state.text : "";

        public static string BusyText => _busy != null && _busy.gameObject.activeSelf ? _busy.text : "";

        public static bool BuildEnabled => _build != null && _build.interactable;

        /// <summary>The mode lit in the middle: 0 Select, 1 Add, 2 Move, 3 Copy.</summary>
        public static int Mode => _modes != null ? _modes.Current : -1;

        public static bool MenuOpen => _menu != null && _menu.gameObject.activeSelf;

        /// <summary>Presses Build in world, exactly as a click on it does.</summary>
        public static void ClickBuild()
        {
            if (_build != null && _build.interactable)
            {
                _build.onClick.Invoke();
            }
        }

        public static void Ensure(RectTransform host)
        {
            if (host == null || (_host == host && _root != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _host = host;
            _generation = UiTheme.Generation;
            Build(host);
        }

        public static void Tick()
        {
            if (_root == null)
            {
                return;
            }

            EditorCommands.Tick();
            var document = EditorState.Document;
            _name.text = document == null ? "No blueprint" : string.IsNullOrEmpty(document.Name) ? "New blueprint" : document.Name;
            _dirty.enabled = document != null && document.Dirty;
            var state = document == null ? ""
                : document.ReadOnly ? "read only"
                : string.IsNullOrEmpty(document.SourcePath) ? "not saved yet"
                : FileNameOf(document.SourcePath);
            if (_state.text != state)
            {
                _state.text = state;
            }

            _stateBack.gameObject.SetActive(state.Length > 0);

            _undo.interactable = document != null && document.CanUndo;
            _redo.interactable = document != null && document.CanRedo;
            Kit.LabelOf(_undo).color = _undo.interactable ? UiTheme.Text : UiTheme.TextDim;
            Kit.LabelOf(_redo).color = _redo.interactable ? UiTheme.Text : UiTheme.TextDim;
            _build.interactable = document != null && document.Pieces.Count > 0;
            var buildText = UiTheme.TextOnAccent;
            buildText.a = _build.interactable ? 1f : 0.6f;
            Kit.LabelOf(_build).color = buildText;
            FitIslands();
            KeepMenuOnScreen();

            var mode = EditorState.Mode != EditMode.Place ? (QuickAdd.IsOpen ? 1 : 0)
                : EditorState.Action == PlaceAction.Add ? 1
                : EditorState.Action == PlaceAction.Move ? 2
                : 3;
            if (_modes.Current != mode)
            {
                _modes.Set(mode);
            }

            var busy = EditorCommands.Busy;
            _busy.gameObject.SetActive(busy != null);
            if (busy != null && busy != _busyShown)
            {
                _busyShown = busy;
                _busy.text = busy + "…";
            }
        }

        private static string _busyShown;
        private static string _pathShown;
        private static string _nameShown = "";

        /// <summary>The file's name, worked out again only when the path changed.</summary>
        private static string FileNameOf(string path)
        {
            if (path != _pathShown)
            {
                _pathShown = path;
                _nameShown = Path.GetFileName(path);
            }

            return _nameShown;
        }

        /// <summary>
        /// The three islands share the screen's width: when they would run into each other (a narrow
        /// window, a big interface scale) all three shrink a little, down to 60 percent.
        /// </summary>
        private static void FitIslands()
        {
            var room = EditorWindow.Root.rect.width - (4f * EditorWindow.Margin);
            var wide = 0f;
            foreach (var island in _islands)
            {
                wide += LayoutUtility.GetPreferredWidth(island);
            }

            var scale = wide > room && wide > 1f ? Mathf.Clamp(room / wide, 0.6f, 1f) : 1f;
            foreach (var island in _islands)
            {
                if (!Mathf.Approximately(island.localScale.x, scale))
                {
                    island.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        /// <summary>
        /// While the menu is open, its card is moved back inside the screen if any part of it is outside: it
        /// opens under the menu button, and nothing the screen's size or the interface scale does may push
        /// it off an edge. Measured on the real card, so it holds whatever the cause.
        /// </summary>
        private static void KeepMenuOnScreen()
        {
            if (!MenuOpen || _menuCard == null || EditorWindow.Root == null)
            {
                return;
            }

            var root = EditorWindow.Root;
            var corners = new Vector3[4];
            _menuCard.GetWorldCorners(corners);
            var low = root.InverseTransformPoint(corners[0]);
            var high = root.InverseTransformPoint(corners[2]);
            var rect = root.rect;
            var margin = EditorWindow.Margin;
            var dx = Mathf.Max(0f, (rect.xMin + margin) - low.x) - Mathf.Max(0f, high.x - (rect.xMax - margin));
            var dy = Mathf.Max(0f, (rect.yMin + margin) - low.y) - Mathf.Max(0f, high.y - (rect.yMax - margin));
            if (Mathf.Abs(dx) > 0.5f || Mathf.Abs(dy) > 0.5f)
            {
                _menu.anchoredPosition += new Vector2(dx, dy);
            }
        }

        /// <summary>Where the menu card is on the screen, in screen pixels. For the tests.</summary>
        public static Rect MenuBox
        {
            get
            {
                if (_menuCard == null)
                {
                    return new Rect();
                }

                var corners = new Vector3[4];
                _menuCard.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
        }

        /// <summary>Opens or closes the menu under the menu button.</summary>
        public static void ToggleMenu()
        {
            if (_menu == null)
            {
                return;
            }

            var open = !_menu.gameObject.activeSelf;
            _menu.gameObject.SetActive(open);
            if (open)
            {
                _menu.SetAsLastSibling();
                foreach (var tag in MenuTags)
                {
                    tag.Key.text = Keymap.Describe(tag.Value);
                }

                // As tall as the items, or as the screen allows: then the list scrolls.
                var room = EditorWindow.Root.rect.height - EditorWindow.TopBand - 24f;
                var wanted = LayoutUtility.GetPreferredHeight(_menuScroll.content) + 8f;
                _menuCard.sizeDelta = new Vector2(0f, Mathf.Min(wanted, Mathf.Max(120f, room)));
            }
        }

        /// <summary>Closes the menu. True when it was open: the Esc ladder's first step.</summary>
        public static bool CloseMenu()
        {
            if (!MenuOpen)
            {
                return false;
            }

            _menu.gameObject.SetActive(false);
            return true;
        }

        private static void PickMode(int mode)
        {
            switch (mode)
            {
                case 0:
                    QuickAdd.Close();
                    EditorState.CancelMode();
                    break;
                case 1:
                    QuickAdd.Toggle();
                    break;
                case 2:
                    if (!EditorState.StartMove())
                    {
                        Toasts.Info("Select something to move first.");
                    }

                    break;
                default:
                    if (!EditorState.StartDuplicate())
                    {
                        Toasts.Info("Select something to copy first.");
                    }

                    break;
            }
        }

        // ---------- widgets ----------

        private static void Build(RectTransform host)
        {
            Walk.Clear();
            MenuTags.Clear();
            _islands.Clear();
            _root = UiBuild.Rect("HeaderContent", host);
            UiBuild.Stretch(_root);

            // Left: the menu button and the blueprint.
            var left = Island("File", 0f);
            _menuButton = Kit.Ghost(left, "Tømrer", ToggleMenu, 32f);
            var mark = UiBuild.Panel("Mark", _menuButton.transform, null, UiTheme.Accent);
            mark.type = Image.Type.Simple;
            UiBuild.Rounded(mark);
            mark.raycastTarget = false;
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            mark.rectTransform.pivot = new Vector2(0f, 0.5f);
            mark.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            mark.rectTransform.sizeDelta = new Vector2(10f, 10f);
            var menuLabel = Kit.LabelOf(_menuButton);
            menuLabel.fontStyle = FontStyles.Bold;
            UiBuild.Stretch(menuLabel.rectTransform, 26f, 0f, 8f, 0f);
            _menuButton.GetComponent<LabelWidth>().Pad = 36f;
            Walk.Add(_menuButton);

            Kit.Divider(left, true);
            _name = Kit.Text(left, "", Kit.BodySize, UiTheme.Text);
            _name.fontStyle = FontStyles.Bold;
            _name.overflowMode = TextOverflowModes.Ellipsis;
            Kit.Size(_name, -1f, 24f);
            var nameWidth = _name.gameObject.AddComponent<LabelWidth>();
            nameWidth.Label = _name;
            nameWidth.Pad = 6f;
            nameWidth.Max = 220f;
            nameWidth.Priority = 2;
            _dirty = UiBuild.Panel("Dirty", left, null, UiTheme.Accent);
            UiBuild.Rounded(_dirty);
            _dirty.type = Image.Type.Sliced;
            _dirty.raycastTarget = false;
            Kit.Size(_dirty, 6f, 6f);
            _state = Kit.Chip(left, "", out _stateBack);
            _state.overflowMode = TextOverflowModes.Ellipsis;
            _stateBack.GetComponent<LabelWidth>().Max = 170f;
            _stateBack.GetComponent<LabelWidth>().Priority = 2;
            _busy = Kit.Text(left, "", Kit.CaptionSize, UiTheme.Accent);
            Kit.Size(_busy, -1f, 24f);
            var busyWidth = _busy.gameObject.AddComponent<LabelWidth>();
            busyWidth.Label = _busy;
            busyWidth.Pad = 8f;
            busyWidth.Max = 160f;
            busyWidth.Priority = 2;
            _busy.gameObject.SetActive(false);
            Pad(left, 6);

            // Middle: the modes.
            var middle = Island("Modes", 0.5f);
            _modes = Kit.Segmented(middle, ModeNames, PickMode, 32f);
            for (var i = 0; i < _modes.Labels.Count; i++)
            {
                Walk.Add(_modes.Buttons[i]);
            }

            _modes.Set(0);

            // Right: the actions, and Build last.
            var right = Island("Actions", 1f);
            _undo = Add(right, Kit.Ghost(right, "Undo", () => EditorState.Undo(), 32f));
            _redo = Add(right, Kit.Ghost(right, "Redo", () => EditorState.Redo(), 32f));
            Kit.Divider(right, true);
            Add(right, Kit.Ghost(right, "Commands", Dialogs.Commands, 32f));
            Add(right, Kit.Ghost(right, "Help", EditorCommands.Help, 32f));
            _build = Add(right, Kit.Primary(right, "Build in world", () => EditorCommands.BuildThis(), 32f));
            Pad(right, 2);

            BuildMenu();

            // The pad walks the bar left and right, around the ends.
            UiBuild.LinkRow(Walk, true);
        }

        private static Button Add(RectTransform row, Button button)
        {
            Walk.Add(button);
            return button;
        }

        /// <summary>
        /// One island of the top band: an opaque card as wide as its children, <see cref="EditorWindow.HeaderHeight"/>
        /// tall, a margin from the top and from its edge (0 left, 0.5 middle, 1 right).
        /// </summary>
        private static RectTransform Island(string name, float side)
        {
            var card = UiBuild.Card(name, _root);
            var rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 1f);
            rect.anchoredPosition = new Vector2(
                side < 0.25f ? EditorWindow.Margin : side > 0.75f ? -EditorWindow.Margin : 0f,
                -EditorWindow.Margin);
            rect.sizeDelta = new Vector2(10f, EditorWindow.HeaderHeight);
            var row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 2f;
            row.padding = new RectOffset(4, 4, 4, 4);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Kit.Fit(rect, true, false);
            _islands.Add(rect);
            return rect;
        }

        /// <summary>A little air at the end of an island, so its last widget is not against the edge.</summary>
        private static void Pad(RectTransform island, int right)
        {
            var row = island.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(row.padding.left, row.padding.right + right, row.padding.top, row.padding.bottom);
        }

        /// <summary>
        /// The menu: a card under the menu button with the file and window commands, each with its keys.
        /// It hangs under the header, so the pad walk reaches it with the header. A click beside it closes it.
        /// </summary>
        private static void BuildMenu()
        {
            // A child of the header frame, not of the island: an island's layout would take it over.
            _menu = UiBuild.Rect("Menu", _root);
            _menu.anchorMin = _menu.anchorMax = new Vector2(0f, 1f);
            _menu.pivot = new Vector2(0f, 1f);
            _menu.anchoredPosition = new Vector2(EditorWindow.Margin, -(EditorWindow.TopBand + 6f));
            _menu.sizeDelta = new Vector2(270f, 10f);

            var catcher = UiBuild.Panel("Beside", _menu, null, new Color(0f, 0f, 0f, 0f));
            catcher.type = Image.Type.Simple;
            catcher.rectTransform.anchorMin = Vector2.zero;
            catcher.rectTransform.anchorMax = Vector2.one;
            catcher.rectTransform.offsetMin = new Vector2(-10000f, -10000f);
            catcher.rectTransform.offsetMax = new Vector2(10000f, 10000f);
            catcher.gameObject.AddComponent<ClickEvents>().Clicked = _ => CloseMenu();

            var card = UiBuild.Card("Card", _menu);
            _menuCard = card.rectTransform;
            _menuCard.anchorMin = new Vector2(0f, 1f);
            _menuCard.anchorMax = new Vector2(1f, 1f);
            _menuCard.pivot = new Vector2(0.5f, 1f);
            _menuCard.anchoredPosition = Vector2.zero;

            // The items scroll when the screen is too short for all of them.
            _menuScroll = Kit.Scroll(card.transform, 0f, 4);
            UiBuild.Stretch((RectTransform)_menuScroll.transform);
            var list = _menuScroll.content;

            Item(list, "New blueprint", Act.None, EditorCommands.NewBlueprint);
            Item(list, "Open…", Act.None, EditorCommands.OpenDialog);
            Item(list, "Save", Act.Save, () => EditorCommands.Save());
            Item(list, "Save as…", Act.None, () => Dialogs.SaveAs(EditorState.Document != null ? EditorState.Document.Name : "New blueprint"));
            Kit.Divider(list);
            Item(list, "Center the origin", Act.None, EditorCommands.CenterOrigin);
            Item(list, "Build in world", Act.None, () => EditorCommands.BuildThis());
            Kit.Divider(list);
            Item(list, "Layers card", Act.ToggleLayers, () => EditorWindow.SetLayers(!EditorWindow.LayersOpen));
            Item(list, "Inspector card", Act.ToggleInspector, () => EditorWindow.SetInspector(!EditorWindow.InspectorOpen));
            Item(list, "Hide the interface", Act.HideUi, () => EditorWindow.SetUiHidden(true));
            Item(list, "Look at the selection", Act.Frame, ViewportHost.Frame);
            Item(list, "Perspective / orthographic", Act.ToggleOrtho, ViewportHost.ToggleOrtho);
            Item(list, "Show only the selection", Act.Isolate, EditorState.IsolateSelection);
            Kit.Divider(list);
            Item(list, "Keys…", Act.None, Dialogs.Controls);
            Item(list, "Search commands", Act.Commands, Dialogs.Commands);
            Item(list, "Help", Act.Help, EditorCommands.Help);
            Kit.Divider(list);
            Item(list, "Close the editor", Act.None, EditorSession.Close, EditorConfig.Key != null ? EditorConfig.Key.Value.ToString() : "F7");

            _menu.gameObject.SetActive(false);
        }

        /// <summary>
        /// One line of the menu: what it does on the left, its keys on the right (read again each time the
        /// menu opens, so a changed key shows). Running it closes the menu.
        /// </summary>
        private static void Item(Transform parent, string text, Act act, UnityEngine.Events.UnityAction run, string fixedKeys = "")
        {
            var button = Kit.Ghost(parent, text, () =>
            {
                CloseMenu();
                run();
            }, 30f);
            var label = Kit.LabelOf(button);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiBuild.Stretch(label.rectTransform, 10f, 0f, 90f, 0f);
            var tag = Kit.Text(button.transform, fixedKeys, Kit.CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            UiBuild.Stretch(tag.rectTransform, 120f, 0f, 10f, 0f);
            if (act != Act.None)
            {
                MenuTags.Add(new KeyValuePair<TextMeshProUGUI, Act>(tag, act));
                tag.text = Keymap.Describe(act);
            }

            Walk.Add(button);
        }

        private static readonly List<KeyValuePair<TextMeshProUGUI, Act>> MenuTags = new List<KeyValuePair<TextMeshProUGUI, Act>>();
    }
}
