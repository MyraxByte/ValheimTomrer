using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's screen, laid out like a design tool:
    /// <code>
    ///   header (menu, blueprint name, modes, actions, Build)
    ///   Layers | 3D view (status, hints, the floating toolbar) | Inspector
    /// </code>
    /// The two side panels are docked to the screen's edges and fold away (Alt+1, Alt+2); the view
    /// takes the room they leave. Ctrl+\ hides everything but the view.
    ///
    /// The canvas is a root canvas of its own, not a child of the game's HUD: the HUD's canvas can be
    /// larger than the screen, which cut the old window's edges off. A root overlay canvas is always
    /// exactly the screen. Built the first time the editor opens and again after a world load or a
    /// theme change.
    ///
    /// A folded panel is slid off the screen, never switched off: the panels measure text while they
    /// build and tick, and TextMeshPro measures nothing in an inactive object. The panel walk leaves
    /// a folded panel out (<see cref="LeftShown"/>, <see cref="RightShown"/>).
    /// </summary>
    internal static class EditorWindow
    {
        public const int SortOrder = 950;
        public const int GroupPriority = 5;

        public const float HeaderHeight = 44f;
        public const float LeftWidth = 248f;
        public const float RightWidth = 292f;
        private const float Away = 6000f;

        /// <summary>How far below the view's top edge text drawn over the view starts.</summary>
        public const float TopInset = 12f;

        /// <summary>Room for the hints and the toolbar along the bottom of the view.</summary>
        public const float BottomInset = 64f;

        private static GameObject _root;
        private static int _generation = -1;
        private static bool _uiHidden;

        /// <summary>The whole canvas. Dialogs, popups and toasts hang here, over everything else.</summary>
        public static RectTransform Root { get; private set; }

        public static RectTransform Header { get; private set; }

        public static RectTransform LeftDock { get; private set; }

        public static RectTransform RightDock { get; private set; }

        /// <summary>The 3D view's region: the space between the docks, under the header.</summary>
        public static RectTransform ViewportHost { get; private set; }

        /// <summary>The floating toolbar along the bottom of the view.</summary>
        public static RectTransform Toolbar { get; private set; }

        /// <summary>The line in the view's top right corner: the blueprint, its pieces, the selection.</summary>
        public static TextMeshProUGUI StatusText { get; private set; }

        public static bool Visible => _root != null && _root.activeSelf;

        /// <summary>Everything but the view is hidden (Ctrl+\, L2 + L3).</summary>
        public static bool UiHidden => _uiHidden;

        public static bool LayersOpen => EditorConfig.LayersOpen == null || EditorConfig.LayersOpen.Value;

        public static bool InspectorOpen => EditorConfig.InspectorOpen == null || EditorConfig.InspectorOpen.Value;

        /// <summary>The Layers panel is on screen, so the panel walk may enter it.</summary>
        public static bool LeftShown => !_uiHidden && LayersOpen;

        /// <summary>The Inspector is on screen.</summary>
        public static bool RightShown => !_uiHidden && InspectorOpen;

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
            Root = Header = LeftDock = RightDock = ViewportHost = Toolbar = null;
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
        /// Puts every region where the switches say: a folded panel off screen, the view stretched over
        /// the room it left, the header and the toolbar away while the interface is hidden.
        /// </summary>
        public static void ApplyLayout()
        {
            if (Header == null)
            {
                return;
            }

            var top = _uiHidden ? 0f : HeaderHeight;
            var left = LeftShown ? LeftWidth : 0f;
            var right = RightShown ? RightWidth : 0f;

            Header.anchoredPosition = _uiHidden ? new Vector2(0f, Away) : Vector2.zero;
            LeftDock.anchoredPosition = LeftShown ? Vector2.zero : new Vector2(-Away, 0f);
            RightDock.anchoredPosition = RightShown ? Vector2.zero : new Vector2(Away, 0f);
            LeftDock.offsetMax = new Vector2(LeftDock.offsetMax.x, -HeaderHeight);
            RightDock.offsetMax = new Vector2(RightDock.offsetMax.x, -HeaderHeight);

            ViewportHost.offsetMin = new Vector2(left, 0f);
            ViewportHost.offsetMax = new Vector2(-right, -top);
            if (Toolbar != null)
            {
                Toolbar.anchoredPosition = _uiHidden ? new Vector2(0f, -Away) : new Vector2(0f, 14f);
            }
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
            // The view first, so everything else draws over it.
            var view = UiBuild.Panel("ViewportHost", root, null, UiTheme.Viewport);
            view.type = Image.Type.Simple;
            ViewportHost = view.rectTransform;
            UiBuild.Stretch(ViewportHost);

            StatusText = Kit.Text(ViewportHost, "", Kit.CaptionSize, UiTheme.TextOnPicture, TextAlignmentOptions.TopRight);
            var status = StatusText.rectTransform;
            status.anchorMin = status.anchorMax = new Vector2(1f, 1f);
            status.pivot = new Vector2(1f, 1f);
            status.anchoredPosition = new Vector2(-14f, -TopInset);
            status.sizeDelta = new Vector2(640f, 18f);

            Toolbar = UiBuild.Rect("ToolbarHost", ViewportHost);
            Toolbar.anchorMin = Toolbar.anchorMax = new Vector2(0.5f, 0f);
            Toolbar.pivot = new Vector2(0.5f, 0f);
            Toolbar.sizeDelta = new Vector2(10f, 40f);

            LeftDock = Dock("LeftDock", root, true);
            RightDock = Dock("RightDock", root, false);

            var header = UiBuild.Panel("Header", root, null, UiTheme.PanelFloat);
            header.type = Image.Type.Simple;
            Header = header.rectTransform;
            Header.anchorMin = new Vector2(0f, 1f);
            Header.anchorMax = new Vector2(1f, 1f);
            Header.pivot = new Vector2(0.5f, 1f);
            Header.offsetMin = new Vector2(0f, -HeaderHeight);
            Header.offsetMax = Vector2.zero;
            var line = Kit.Divider(Header);
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 1f);

            ApplyLayout();
        }

        /// <summary>A panel docked to the left or the right edge, from under the header to the bottom, with a line on its inner side.</summary>
        private static RectTransform Dock(string name, RectTransform root, bool left)
        {
            var panel = UiBuild.Panel(name, root, null, UiTheme.PanelFloat);
            panel.type = Image.Type.Simple;
            var rect = panel.rectTransform;
            var side = left ? 0f : 1f;
            rect.anchorMin = new Vector2(side, 0f);
            rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 0.5f);
            var width = left ? LeftWidth : RightWidth;
            rect.offsetMin = new Vector2(left ? 0f : -width, 0f);
            rect.offsetMax = new Vector2(left ? width : 0f, -HeaderHeight);

            var edge = Kit.Divider(rect, true);
            edge.rectTransform.anchorMin = new Vector2(left ? 1f : 0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(left ? 1f : 0f, 1f);
            edge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(1f, 0f);
            Object.Destroy(edge.GetComponent<LayoutElement>());
            return rect;
        }
    }
}
