using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's screen: the 3D view fills the whole screen, and the interface floats over it as
    /// islands, opaque rounded cards with a margin round them:
    /// <code>
    ///   [menu, name]      [Select Add Move Copy]      [undo, redo, ... Build]
    ///   [Layers]                                      [Inspector]
    ///                     [grid, turn, snap ...]
    /// </code>
    /// Every island can be put away: the Layers and Inspector cards with their Hide button, Alt+1 and
    /// Alt+2 (a small tab on the edge brings one back), all of them with Ctrl+\ (the view stays). The
    /// view never changes size when an island moves, so the picture does not jump.
    ///
    /// The canvas is a root canvas of its own, not a child of the game's HUD: the HUD's canvas can be
    /// larger than the screen, which cut the old window's edges off. A root overlay canvas is always
    /// exactly the screen. Built the first time the editor opens and again after a world load or a
    /// theme change.
    ///
    /// Draw order, bottom to top: the view, the toolbar and the status line, the two cards, the header
    /// islands, the edge tabs. A put-away island is slid off the screen, never switched off: the
    /// panels measure text while they build and tick, and TextMeshPro measures nothing in an inactive
    /// object. The panel walk leaves a put-away island out (<see cref="LeftShown"/>, <see cref="RightShown"/>).
    /// </summary>
    internal static class EditorWindow
    {
        public const int SortOrder = 950;
        public const int GroupPriority = 5;

        /// <summary>The gap between an island and the screen's edge, and between two islands.</summary>
        public const float Margin = 10f;

        /// <summary>The height of a header island.</summary>
        public const float HeaderHeight = 40f;

        /// <summary>The top band the header islands sit in.</summary>
        public const float TopBand = Margin + HeaderHeight;

        /// <summary>Where the cards start, from the top.</summary>
        public const float DockTop = TopBand + Margin;

        public const float LeftWidth = 256f;
        public const float RightWidth = 300f;
        private const float Away = 6000f;

        /// <summary>The height of the floating toolbar's host.</summary>
        public const float ToolbarHeight = 40f;

        private static GameObject _root;
        private static int _generation = -1;
        private static bool _uiHidden;

        /// <summary>The whole canvas. Dialogs, popups and toasts hang here, over everything else.</summary>
        public static RectTransform Root { get; private set; }

        public static RectTransform Header { get; private set; }

        public static RectTransform LeftDock { get; private set; }

        public static RectTransform RightDock { get; private set; }

        /// <summary>The 3D view's region: the whole screen, always.</summary>
        public static RectTransform ViewportHost { get; private set; }

        /// <summary>The floating toolbar along the bottom of the view.</summary>
        public static RectTransform Toolbar { get; private set; }

        /// <summary>The line in the view's top right corner: the blueprint, its pieces, the selection.</summary>
        public static TextMeshProUGUI StatusText { get; private set; }

        /// <summary>The small tab on the left edge that brings the Layers card back.</summary>
        public static RectTransform LayersTab { get; private set; }

        /// <summary>The small tab on the right edge that brings the Inspector back.</summary>
        public static RectTransform InspectorTab { get; private set; }

        public static bool Visible => _root != null && _root.activeSelf;

        /// <summary>Everything but the view is hidden (Ctrl+\, L2 + L3).</summary>
        public static bool UiHidden => _uiHidden;

        public static bool LayersOpen => EditorConfig.LayersOpen == null || EditorConfig.LayersOpen.Value;

        public static bool InspectorOpen => EditorConfig.InspectorOpen == null || EditorConfig.InspectorOpen.Value;

        /// <summary>The Layers panel is on screen, so the panel walk may enter it.</summary>
        public static bool LeftShown => !_uiHidden && LayersOpen;

        /// <summary>The Inspector is on screen.</summary>
        public static bool RightShown => !_uiHidden && InspectorOpen;

        /// <summary>
        /// The room the islands leave for what is drawn over the view, in canvas units from each edge.
        /// Hints, the toolbar and the status line sit inside it, so they never hide under a card.
        /// </summary>
        public static float FreeLeft => LeftShown ? Margin + LeftWidth + Margin : Margin;

        public static float FreeRight => RightShown ? Margin + RightWidth + Margin : Margin;

        public static float FreeTop => _uiHidden ? Margin : DockTop;

        /// <summary>Room for the hints and the toolbar along the bottom.</summary>
        public static float FreeBottom => _uiHidden ? Margin : Margin + ToolbarHeight + 14f;

        /// <summary>
        /// Room an edge tab takes at the top of that side (the Layers tab on the left, the Inspector's
        /// on the right), so the lines drawn there start under it. Zero while the card itself is out.
        /// </summary>
        public static float TabRoom(bool right)
        {
            var folded = right ? !InspectorOpen : !LayersOpen;
            return folded && !_uiHidden ? 40f : 0f;
        }

        /// <summary>How far the middle of the free room is right of the screen's middle.</summary>
        public static float FreeShift => (FreeLeft - FreeRight) * 0.5f;

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
            _root = CreateRoot("ValheimTomrerEditor", SortOrder);
            Root = (RectTransform)_root.transform;
            _generation = UiTheme.Generation;

            // Built switched on: TextMeshPro measures nothing in an inactive object, and every button
            // takes its width from its words. The caller shows the window in the same frame.
            _root.SetActive(true);
            Build(Root);
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
            Root = Header = LeftDock = RightDock = ViewportHost = Toolbar = LayersTab = InspectorTab = null;
            StatusText = null;
            _generation = -1;
        }

        /// <summary>Folds the Layers panel in or out. Remembered in the config.</summary>
        public static void SetLayers(bool open)
        {
            if (EditorConfig.LayersOpen != null && EditorConfig.LayersOpen.Value != open)
            {
                EditorConfig.LayersOpen.Value = open;
            }

            ApplyLayout();
        }

        /// <summary>Folds the Inspector in or out. Remembered in the config.</summary>
        public static void SetInspector(bool open)
        {
            if (EditorConfig.InspectorOpen != null && EditorConfig.InspectorOpen.Value != open)
            {
                EditorConfig.InspectorOpen.Value = open;
            }

            ApplyLayout();
        }

        /// <summary>Hides the header, both panels and the toolbar, or brings them back. The view stays.</summary>
        public static void SetUiHidden(bool hidden)
        {
            _uiHidden = hidden;
            if (hidden)
            {
                FocusNav.Leave();
            }

            ApplyLayout();
        }

        /// <summary>
        /// Puts every island where the switches say: a put-away one off screen, the header and the
        /// toolbar away while the interface is hidden, the lines drawn over the view inside the room
        /// that is left. Every position is set from offsets or anchors alone, the same way every
        /// time, so calling it twice changes nothing.
        /// </summary>
        public static void ApplyLayout()
        {
            if (Header == null)
            {
                return;
            }

            var lift = _uiHidden ? Away : 0f;
            Header.offsetMin = new Vector2(0f, -TopBand + lift);
            Header.offsetMax = new Vector2(0f, lift);
            Dock(LeftDock, true, LeftShown);
            Dock(RightDock, false, RightShown);
            Toolbar.anchoredPosition = _uiHidden ? new Vector2(0f, -Away) : new Vector2(FreeShift, Margin);
            StatusText.rectTransform.anchoredPosition = new Vector2(-FreeRight, -(FreeTop - 4f + TabRoom(true)));
            LayersTab.gameObject.SetActive(!_uiHidden && !LayersOpen);
            InspectorTab.gameObject.SetActive(!_uiHidden && !InspectorOpen);
            Ui.ViewportHost.Relayout();
        }

        /// <summary>
        /// A root overlay canvas, the size of the screen. CanvasScaler (reference pixels per unit 50)
        /// before GuiScaler, which follows the game's own interface scale; CanvasGroup before
        /// UIGroupHandler. Sort order 950 puts it over the inventory and under the pause menu.
        /// </summary>
        private static GameObject CreateRoot(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.SetActive(false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 50f;
            go.AddComponent<GuiScaler>();
            go.AddComponent<GraphicRaycaster>();

            go.AddComponent<CanvasGroup>();
            go.AddComponent<UIGroupHandler>().m_groupPriority = GroupPriority;
            return go;
        }

        private static void Build(RectTransform root)
        {
            // The view first, so everything else draws over it: the whole screen.
            var view = UiBuild.Panel("ViewportHost", root, null, UiTheme.Viewport);
            view.type = Image.Type.Simple;
            ViewportHost = view.rectTransform;
            UiBuild.Stretch(ViewportHost);

            // Over the view, under the islands. They hang on the root, never on the view: the view's
            // own picture is drawn after anything built into it.
            StatusText = Kit.Text(root, "", Kit.CaptionSize, UiTheme.TextOnPicture, TextAlignmentOptions.TopRight);
            UiBuild.OverPicture(StatusText);
            var status = StatusText.rectTransform;
            status.anchorMin = status.anchorMax = new Vector2(1f, 1f);
            status.pivot = new Vector2(1f, 1f);
            status.sizeDelta = new Vector2(640f, 18f);

            Toolbar = UiBuild.Rect("ToolbarHost", root);
            Toolbar.anchorMin = Toolbar.anchorMax = new Vector2(0.5f, 0f);
            Toolbar.pivot = new Vector2(0.5f, 0f);
            Toolbar.sizeDelta = new Vector2(10f, ToolbarHeight);

            LeftDock = NewDock("LeftDock", root, true);
            RightDock = NewDock("RightDock", root, false);

            // The edge tabs are under the header, so the header's menu draws over them and takes the clicks.
            LayersTab = Tab("LayersTab", root, "Layers", true, () => SetLayers(true));
            InspectorTab = Tab("InspectorTab", root, "Inspector", false, () => SetInspector(true));

            // The header: only a frame for the islands that Header builds into it. It has no picture,
            // so a click between the islands goes through to the view.
            Header = UiBuild.Rect("Header", root);
            Header.anchorMin = new Vector2(0f, 1f);
            Header.anchorMax = new Vector2(1f, 1f);
            Header.pivot = new Vector2(0.5f, 1f);

            ApplyLayout();
        }

        /// <summary>A card for the left or the right edge: from under the header to the bottom, a margin round it.</summary>
        private static RectTransform NewDock(string name, RectTransform root, bool left)
        {
            var card = UiBuild.Card(name, root);
            var rect = card.rectTransform;
            var side = left ? 0f : 1f;
            rect.anchorMin = new Vector2(side, 0f);
            rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 0.5f);
            Dock(rect, left, true);
            return rect;
        }

        /// <summary>Sets a card's place: its margin from the edges, or far off screen when it is put away.</summary>
        private static void Dock(RectTransform rect, bool left, bool shown)
        {
            var width = left ? LeftWidth : RightWidth;
            var slide = shown ? 0f : left ? -Away : Away;
            var near = left ? Margin : -(Margin + width);
            var far = left ? Margin + width : -Margin;
            rect.offsetMin = new Vector2(near + slide, Margin);
            rect.offsetMax = new Vector2(far + slide, -DockTop);
        }

        /// <summary>The small tab that stands where a put-away card was: a click brings the card back.</summary>
        private static RectTransform Tab(string name, RectTransform root, string text, bool left, UnityEngine.Events.UnityAction open)
        {
            var card = UiBuild.Card(name, root);
            var rect = card.rectTransform;
            var side = left ? 0f : 1f;
            rect.anchorMin = rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 1f);
            rect.anchoredPosition = new Vector2(left ? Margin : -Margin, -DockTop);
            rect.sizeDelta = new Vector2(10f, 32f);
            var row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(3, 3, 3, 3);
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            Kit.Fit(rect, true, false);
            Kit.Ghost(card.transform, text, open, 26f);
            return rect;
        }
    }
}
