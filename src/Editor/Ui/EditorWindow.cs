using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's canvas. The 3D view fills the whole screen and everything else floats over it
    /// and gets out of the way: a thin top bar, two cards that fold away (Layers on the left,
    /// Inspector on the right), a small status line, and Quick add, a popup that Tab opens in the
    /// middle. Ctrl+\ hides all of it. Built the first time it is opened and again after a world
    /// load or a theme change, because the canvas dies with the scene.
    ///
    /// The recipe is the vanilla one: own Canvas with overrideSorting, CanvasScaler (reference
    /// pixels per unit 50) before GuiScaler, CanvasGroup before UIGroupHandler. Sort order 950
    /// puts it over the inventory and the store, under the centre messages and the pause menu.
    ///
    /// A card that is folded away is slid off the screen, not switched off: the panels measure
    /// text while they build and while they tick, and TextMeshPro measures nothing in an inactive
    /// object. The panel walk leaves a folded card out (<see cref="LeftShown"/>).
    /// </summary>
    internal static class EditorWindow
    {
        public const int SortOrder = 950;
        public const int GroupPriority = 5;

        private const float Edge = 8f;      // card to screen edge
        private const float Gap = 8f;       // region to region
        private const float TopBarHeight = 36f;
        private const float StatusBarHeight = 22f;
        private const float LeftWidth = 260f;
        private const float RightWidth = 300f;
        private const float BlueprintHeight = 368f;
        private const float SelectionHeight = 252f;
        private const float PaneEdge = 6f;  // right card frame to its three regions
        private const float PopupWidth = 760f;
        private const float PopupHeight = 600f;
        private const float HandleWidth = 22f;
        private const float HandleHeight = 72f;
        private const float Away = 6000f;   // how far a folded card slides

        /// <summary>How far down from the top of the screen the free view starts. Text drawn over the view keeps below it.</summary>
        public const float TopInset = TopBarHeight + 6f;

        /// <summary>Clearance for the hint row along the bottom of the view.</summary>
        public const float BottomInset = 44f;

        /// <summary>The problem list keeps at least this much when the blueprint region grows.</summary>
        private const float ChecksMinHeight = 160f;

        /// <summary>The blueprint region's height now: <see cref="BlueprintHeight"/>, or more for a long materials list.</summary>
        private static float _blueprintBand = BlueprintHeight;

        private static GameObject _root;
        private static int _generation = -1;
        private static bool _uiHidden;
        private static Button _leftHandle;
        private static Button _rightHandle;
        private static TextMeshProUGUI _leftHandleLabel;
        private static TextMeshProUGUI _rightHandleLabel;

        /// <summary>The whole canvas. Dialogs and toasts hang here, over everything else.</summary>
        public static RectTransform Root { get; private set; }

        public static RectTransform TopBar { get; private set; }
        public static RectTransform LeftPanel { get; private set; }
        public static RectTransform ViewportHost { get; private set; }
        public static RectTransform RightPanel { get; private set; }
        public static RectTransform StatusBar { get; private set; }
        public static TextMeshProUGUI StatusText { get; private set; }

        /// <summary>Where the piece palette lives: inside the Quick add popup.</summary>
        public static RectTransform PalettePane { get; private set; }

        /// <summary>The Layers card's list: one row per piece of the open blueprint.</summary>
        public static RectTransform PieceListPane { get; private set; }

        /// <summary>The Quick add popup's switch: the dim layer over the whole screen. Active means open.</summary>
        public static GameObject PopupHost { get; private set; }

        /// <summary>The Quick add popup's own frame.</summary>
        public static RectTransform Popup { get; private set; }

        /// <summary>0 while Quick add is up (the Pieces list), else 1. Kept for the tests of the old two-tab panel.</summary>
        public static int LeftTab => QuickAdd.IsOpen ? 0 : 1;

        /// <summary>Everything but the view is hidden (Ctrl+\).</summary>
        public static bool UiHidden => _uiHidden;

        public static bool LayersOpen => EditorConfig.LayersOpen != null && EditorConfig.LayersOpen.Value;

        public static bool InspectorOpen => EditorConfig.InspectorOpen == null || EditorConfig.InspectorOpen.Value;

        /// <summary>The Layers card is on screen, so the panel walk may enter it.</summary>
        public static bool LeftShown => !_uiHidden && LayersOpen;

        /// <summary>The Inspector card is on screen.</summary>
        public static bool RightShown => !_uiHidden && InspectorOpen;

        /// <summary>The right panel's three regions, top to bottom.</summary>
        public static RectTransform BlueprintPane { get; private set; }

        public static RectTransform SelectionPane { get; private set; }

        public static RectTransform ChecksPane { get; private set; }

        public static bool Visible => _root != null && _root.activeSelf;

        /// <summary>Builds the window if it is missing. False when the game is not ready for it.</summary>
        public static bool Ensure()
        {
            if (!UiTheme.Ensure())
            {
                return false;
            }

            if (_root != null && _generation == UiTheme.Generation)
            {
                return true;
            }

            Destroy();

            var parent = Hud.instance != null ? Hud.instance.transform.parent : null;
            if (parent == null)
            {
                return false;
            }

            _root = CreateRoot("ValheimTomrerEditor", SortOrder, parent);
            Root = (RectTransform)_root.transform;
            _generation = UiTheme.Generation;
            Build((RectTransform)_root.transform);
            ValheimTomrerPlugin.Log.LogInfo("editor window built");
            return true;
        }

        public static void Show(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        public static void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _root = null;
            Root = null;
            _generation = -1;
            TopBar = LeftPanel = ViewportHost = RightPanel = StatusBar = null;
            PalettePane = PieceListPane = null;
            PopupHost = null;
            Popup = null;
            BlueprintPane = SelectionPane = ChecksPane = null;
            _leftHandle = _rightHandle = null;
            _leftHandleLabel = _rightHandleLabel = null;
            StatusText = null;
        }

        /// <summary>
        /// The old two-tab panel's switch, kept for the autotest: 0 opens Quick add, 1 closes it and
        /// shows the Layers card.
        /// </summary>
        public static void SetLeftTab(int tab)
        {
            if (tab == 0)
            {
                QuickAdd.Open();
                return;
            }

            QuickAdd.Close();
            SetLayers(true);
        }

        /// <summary>Folds the Layers card in or out. Remembered in the config.</summary>
        public static void SetLayers(bool open)
        {
            if (EditorConfig.LayersOpen != null && EditorConfig.LayersOpen.Value != open)
            {
                EditorConfig.LayersOpen.Value = open;
            }

            ApplyCards();
        }

        /// <summary>Folds the Inspector card in or out. Remembered in the config.</summary>
        public static void SetInspector(bool open)
        {
            if (EditorConfig.InspectorOpen != null && EditorConfig.InspectorOpen.Value != open)
            {
                EditorConfig.InspectorOpen.Value = open;
            }

            ApplyCards();
        }

        /// <summary>Hides every card and the top bar, or brings them back. The view stays.</summary>
        public static void SetUiHidden(bool hidden)
        {
            _uiHidden = hidden;
            if (hidden)
            {
                FocusNav.Leave();
            }

            ApplyCards();
        }

        /// <summary>Slides each card in or out of the screen and moves the fold handles with them.</summary>
        public static void ApplyCards()
        {
            if (TopBar == null)
            {
                return;
            }

            Slide(TopBar, !_uiHidden, new Vector2(0f, Away));
            Slide(StatusBar, !_uiHidden, new Vector2(-Away, 0f));
            Slide(LeftPanel, LeftShown, new Vector2(-Away, 0f));
            Slide(RightPanel, RightShown, new Vector2(Away, 0f));

            PlaceHandle(_leftHandle, _leftHandleLabel, true, LayersOpen, LeftWidth);
            PlaceHandle(_rightHandle, _rightHandleLabel, false, InspectorOpen, RightWidth);
        }

        private static void Slide(RectTransform rect, bool shown, Vector2 away)
        {
            if (rect != null)
            {
                rect.anchoredPosition = shown ? Vector2.zero : away;
            }
        }

        private static void PlaceHandle(Button handle, TextMeshProUGUI label, bool left, bool open, float width)
        {
            if (handle == null)
            {
                return;
            }

            handle.gameObject.SetActive(!_uiHidden);
            var rect = (RectTransform)handle.transform;
            var x = open ? Edge + width + 4f : Edge * 0.25f;
            rect.anchoredPosition = new Vector2(left ? x : -x, 0f);
            label.text = left == open ? "‹" : "›";
        }

        /// <summary>
        /// The Quick add popup's frame in the canvas: left and right edges and the top edge, measured
        /// from the canvas's left and top (the top is negative). The piece card hangs beside it.
        /// </summary>
        public static void PopupEdges(out float left, out float right, out float ceiling)
        {
            var frame = Root;
            if (frame == null || Popup == null)
            {
                left = 0f;
                right = 0f;
                ceiling = 0f;
                return;
            }

            var centre = frame.rect.width * 0.5f + Popup.anchoredPosition.x;
            left = centre - Popup.rect.width * 0.5f;
            right = centre + Popup.rect.width * 0.5f;
            ceiling = -(frame.rect.height * 0.5f - Popup.anchoredPosition.y - Popup.rect.height * 0.5f);
        }

        /// <summary>The canvas recipe the game itself uses (SessionPlayerList). Order matters twice.</summary>
        private static GameObject CreateRoot(string name, int order, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            UiBuild.Stretch((RectTransform)go.transform);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 50f;
            go.AddComponent<GuiScaler>();          // reads the CanvasScaler in Awake
            go.AddComponent<GraphicRaycaster>();

            go.AddComponent<CanvasGroup>();        // read by UIGroupHandler in Awake
            go.AddComponent<UIGroupHandler>().m_groupPriority = GroupPriority;
            return go;
        }

        /// <summary>
        /// The view first, full screen, then the top bar, the two cards, the status line, their
        /// fold handles and last the Quick add popup, which sits over all of them.
        /// </summary>
        private static void Build(RectTransform root)
        {
            ViewportHost = Region("ViewportHost", root, null, UiTheme.Viewport);
            UiBuild.Stretch(ViewportHost);

            TopBar = Region("TopBar", root, UiTheme.PanelWood, UiTheme.PanelFloat);
            TopBar.anchorMin = new Vector2(0f, 1f);
            TopBar.anchorMax = new Vector2(1f, 1f);
            TopBar.offsetMin = new Vector2(0f, -TopBarHeight);
            TopBar.offsetMax = Vector2.zero;

            LeftPanel = Region("LeftPanel", root, UiTheme.PanelWood, UiTheme.PanelFloat);
            LeftPanel.anchorMin = new Vector2(0f, 0.34f);
            LeftPanel.anchorMax = new Vector2(0f, 1f);
            LeftPanel.pivot = new Vector2(0.5f, 0.5f);
            LeftPanel.offsetMin = new Vector2(Edge, 0f);
            LeftPanel.offsetMax = new Vector2(Edge + LeftWidth, -(TopBarHeight + Edge));

            RightPanel = Region("RightPanel", root, UiTheme.PanelWood, UiTheme.PanelFloat);
            RightPanel.anchorMin = new Vector2(1f, 0f);
            RightPanel.anchorMax = new Vector2(1f, 1f);
            RightPanel.pivot = new Vector2(0.5f, 0.5f);
            RightPanel.offsetMin = new Vector2(-Edge - RightWidth, BottomInset);
            RightPanel.offsetMax = new Vector2(-Edge, -(TopBarHeight + Edge));

            StatusBar = Region("StatusBar", root, UiTheme.PanelWood, UiTheme.PanelFloat);
            StatusBar.anchorMin = new Vector2(0f, 0f);
            StatusBar.anchorMax = new Vector2(0f, 0f);
            StatusBar.pivot = new Vector2(0.5f, 0.5f);
            StatusBar.offsetMin = new Vector2(Edge, BottomInset - 4f);
            StatusBar.offsetMax = new Vector2(Edge + 560f, BottomInset - 4f + StatusBarHeight);
            StatusText = Caption(StatusBar, "F7 or Esc closes", 15f, TextAlignmentOptions.Left, UiTheme.TextDim);

            BuildLeftCard();
            BuildRightPanes();

            _leftHandle = Handle("LayersHandle", true, () => SetLayers(!LayersOpen), out _leftHandleLabel);
            _rightHandle = Handle("InspectorHandle", false, () => SetInspector(!InspectorOpen), out _rightHandleLabel);

            BuildPopup(root);
            ApplyCards();
        }

        /// <summary>
        /// The blueprint region's height in use. For the tests.
        /// </summary>
        public static float BlueprintBand => _blueprintBand;

        /// <summary>
        /// Gives the blueprint region the height its content wants: never less than it always had,
        /// never so much that the problem list keeps less than <see cref="ChecksMinHeight"/>. The
        /// selection moves down with it and the problem list gives up the room. Past that the region
        /// scrolls. Called once a frame by <see cref="BlueprintPanel"/>; does nothing when the
        /// height stays the same.
        /// </summary>
        public static void FitBlueprint(float wanted)
        {
            if (RightPanel == null || BlueprintPane == null)
            {
                return;
            }

            var most = RightPanel.rect.height - PaneEdge - Gap - SelectionHeight - Gap - ChecksMinHeight - PaneEdge;
            var height = Mathf.Round(Mathf.Max(BlueprintHeight, Mathf.Min(wanted, most)));
            if (Mathf.Approximately(height, _blueprintBand))
            {
                return;
            }

            _blueprintBand = height;
            LayRightPanes();
        }

        /// <summary>
        /// The right panel, top to bottom: the blueprint and its build card, the selection, then
        /// the problem list, which takes whatever is left.
        /// </summary>
        private static void BuildRightPanes()
        {
            BlueprintPane = Band("BlueprintPane");
            SelectionPane = Band("SelectionPane");

            ChecksPane = UiBuild.Rect("ChecksPane", RightPanel);
            ChecksPane.anchorMin = Vector2.zero;
            ChecksPane.anchorMax = Vector2.one;
            LayRightPanes();
        }

        private static void LayRightPanes()
        {
            var blueprintTop = PaneEdge;
            var selectionTop = blueprintTop + _blueprintBand + Gap;
            var checksTop = selectionTop + SelectionHeight + Gap;
            Place(BlueprintPane, blueprintTop, _blueprintBand);
            Place(SelectionPane, selectionTop, SelectionHeight);
            ChecksPane.offsetMin = new Vector2(PaneEdge, PaneEdge);
            ChecksPane.offsetMax = new Vector2(-PaneEdge, -checksTop);
        }

        /// <summary>One region across the right panel, its top edge measured down from the panel's top.</summary>
        private static RectTransform Band(string name)
        {
            var rect = UiBuild.Rect(name, RightPanel);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            return rect;
        }

        private static void Place(RectTransform rect, float top, float height)
        {
            rect.offsetMin = new Vector2(PaneEdge, -top - height);
            rect.offsetMax = new Vector2(-PaneEdge, -top);
        }

        /// <summary>The Layers card: a title over the list of the blueprint's pieces.</summary>
        private static void BuildLeftCard()
        {
            const float Title = 28f;
            var title = UiBuild.Label("Title", LeftPanel, "Layers", 17f, TextAlignmentOptions.Left, UiTheme.Accent);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(12f, -Title - 4f);
            title.rectTransform.offsetMax = new Vector2(-12f, -4f);

            PieceListPane = UiBuild.Rect("PieceListPane", LeftPanel);
            UiBuild.Stretch(PieceListPane, 0f, 0f, 0f, Title + 6f);
        }

        /// <summary>A fold tab on the screen edge, halfway up. Mouse only: the pad and Alt+1, Alt+2 use the top bar.</summary>
        private static Button Handle(string name, bool left, UnityEngine.Events.UnityAction click, out TextMeshProUGUI label)
        {
            var button = UiBuild.Button(name, Root, "", click, HandleHeight);
            var rect = (RectTransform)button.transform;
            var side = left ? 0f : 1f;
            rect.anchorMin = rect.anchorMax = new Vector2(side, 0.5f);
            rect.pivot = new Vector2(side, 0.5f);
            rect.sizeDelta = new Vector2(HandleWidth, HandleHeight);
            label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 22f;
            UiBuild.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

            // Not a stop of the panel walk: it is a mouse shortcut for the top bar's button.
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        /// <summary>
        /// Quick add: a dim layer over the screen that closes the popup when clicked, and the
        /// popup's own frame with the piece palette in it. Built open so its text can be measured
        /// (TextMeshPro measures nothing in an inactive object): <see cref="EditorSession"/> folds
        /// it away once the palette has been built.
        /// </summary>
        private static void BuildPopup(RectTransform root)
        {
            var dim = UiBuild.Panel("QuickAdd", root, null, new Color(0f, 0f, 0f, 0.45f));
            dim.type = Image.Type.Simple;
            UiBuild.Stretch(dim.rectTransform);
            PopupHost = dim.gameObject;
            var click = dim.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.targetGraphic = dim;
            click.onClick.AddListener(() => QuickAdd.Close());
            var navigation = click.navigation;
            navigation.mode = Navigation.Mode.None;
            click.navigation = navigation;

            var frame = UiBuild.Panel("QuickAddFrame", dim.transform, UiTheme.Panel);
            Popup = frame.rectTransform;
            Popup.anchorMin = Popup.anchorMax = new Vector2(0.5f, 0.5f);
            Popup.pivot = new Vector2(0.5f, 0.5f);
            Popup.anchoredPosition = Vector2.zero;
            Popup.sizeDelta = new Vector2(PopupWidth, PopupHeight);

            var title = UiBuild.Label("Title", Popup, "Add a piece", 20f, TextAlignmentOptions.Left, UiTheme.Accent);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(18f, -40f);
            title.rectTransform.offsetMax = new Vector2(-18f, -10f);
            var hint = UiBuild.Label("Hint", Popup, "Type to search   |   Right click a tile to star it   |   Tab or Esc closes",
                13f, TextAlignmentOptions.Right, UiTheme.TextDim);
            hint.rectTransform.anchorMin = new Vector2(0f, 1f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            hint.rectTransform.offsetMin = new Vector2(18f, -38f);
            hint.rectTransform.offsetMax = new Vector2(-18f, -12f);

            PalettePane = UiBuild.Rect("PalettePane", Popup);
            UiBuild.Stretch(PalettePane, 14f, 14f, 14f, 46f);
        }

        private static RectTransform Region(string name, Transform parent, Sprite sprite, Color tint)
        {
            return UiBuild.Panel(name, parent, sprite, tint).rectTransform;
        }

        private static TextMeshProUGUI Caption(RectTransform parent, string text, float size, TextAlignmentOptions align, Color color)
        {
            var label = UiBuild.Label("Caption", parent, text, size, align, color);
            UiBuild.Stretch(label.rectTransform, 10f, 4f, 10f, 4f);
            return label;
        }
    }
}
