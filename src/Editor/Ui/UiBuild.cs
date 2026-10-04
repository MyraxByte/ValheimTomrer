using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// Small builders for the editor's widgets. Every text is TextMeshPro with the font taken
    /// from the HUD (a TMP text with no font draws nothing), and every sprite is drawn sliced
    /// with pixelsPerUnitMultiplier 1, which is what the 50 ppu UIAtlas borders expect.
    /// </summary>
    internal static class UiBuild
    {
        private const int UiLayer = 5;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Fills the parent, with an inset on each side.</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image Panel(string name, Transform parent, Sprite sprite, Color? tint = null)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = tint ?? Color.white;
            return image;
        }

        public static TextMeshProUGUI Label(
            string name,
            Transform parent,
            string text,
            float size = 18f,
            TextAlignmentOptions align = TextAlignmentOptions.Left,
            Color? color = null)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = UiTheme.Font;
            if (UiTheme.FontMaterial != null)
            {
                label.fontSharedMaterial = UiTheme.FontMaterial;
            }

            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? UiTheme.Text;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        /// <summary>
        /// Gives a label the outlined material. For text drawn straight over the 3D picture: the
        /// background there is whatever the camera is pointed at, and white on a light sky or a
        /// pale floor is unreadable without an edge.
        /// </summary>
        public static TextMeshProUGUI OverPicture(TextMeshProUGUI label)
        {
            if (label != null && UiTheme.FontOutlined != null)
            {
                label.fontSharedMaterial = UiTheme.FontOutlined;
            }

            return label;
        }

        /// <summary>
        /// An island: an opaque card with rounded corners, a one-pixel border and a soft shadow. The
        /// editor's panels, bars and popups are all this.
        /// </summary>
        public static Image Card(string name, Transform parent, Color? colour = null, bool shadow = true)
        {
            var image = Panel(name, parent, UiTheme.Round, colour ?? UiTheme.PanelFloat);
            image.type = UiTheme.Round != null ? Image.Type.Sliced : Image.Type.Simple;
            Border(image);
            if (shadow)
            {
                var drop = image.gameObject.AddComponent<Shadow>();
                drop.effectColor = UiTheme.IslandShadow;
                drop.effectDistance = new Vector2(0f, -3f);
                drop.useGraphicAlpha = false;
            }

            return image;
        }

        /// <summary>Gives a flat image the small round corner of a button or a field.</summary>
        public static Image Rounded(Image image)
        {
            if (image != null && UiTheme.RoundSmall != null)
            {
                image.sprite = UiTheme.RoundSmall;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        /// <summary>A one-pixel line round a widget, in the theme's border colour.</summary>
        public static void Border(Graphic graphic)
        {
            if (graphic == null)
            {
                return;
            }

            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        /// <summary>The flat button colours: surface, lighter under the mouse, darker while pressed.</summary>
        public static ColorBlock ButtonColours()
        {
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = UiTheme.Surface;
            colours.highlightedColor = UiTheme.SurfaceHover;
            colours.selectedColor = UiTheme.SurfaceHover;
            colours.pressedColor = UiTheme.SurfacePressed;
            var off = UiTheme.Surface;
            off.a *= 0.45f;
            colours.disabledColor = off;
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.06f;
            return colours;
        }

        public static UnityEngine.UI.Button Button(string name, Transform parent, string text, UnityAction onClick, float height = 38f)
        {
            var image = Panel(name, parent, null, Color.white);
            image.type = Image.Type.Simple;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColours();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            var label = Label("Text", image.transform, text, 15f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 10f, 4f, 10f, 4f);
            return button;
        }

        public static TMP_InputField InputField(string name, Transform parent, string placeholder, float height = 34f)
        {
            var background = Panel(name, parent, null, UiTheme.Field);
            background.type = Image.Type.Simple;
            Rounded(background);
            Border(background);
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            var area = Stretch(Rect("Text Area", background.transform), 10f, 5f, 10f, 5f);
            area.gameObject.AddComponent<RectMask2D>();

            var text = Label("Text", area, string.Empty);
            Stretch(text.rectTransform);
            text.richText = false;

            var hint = Label("Placeholder", area, placeholder, 16f, TextAlignmentOptions.Left, UiTheme.TextDim);
            Stretch(hint.rectTransform);

            var field = background.gameObject.AddComponent<TextBox>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = background;
            field.fontAsset = UiTheme.Font;
            field.pointSize = 16f;
            field.customCaretColor = true;
            field.caretColor = UiTheme.Text;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.text = string.Empty;
            return field;
        }

        /// <summary>A vertical list that grows with its children. Add rows to scroll.content.</summary>
        public static ScrollRect Scroll(string name, Transform parent, float spacing = 4f)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Stretch(Rect("Viewport", root));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // The wheel is read by WheelScroll, not by the ScrollRect: its own step was half a row.
            scroll.scrollSensitivity = 0f;
            scroll.inertia = false;
            root.gameObject.AddComponent<WheelScroll>().Init(scroll);
            return scroll;
        }

        /// <summary>
        /// Walks a row of buttons left and right with the pad, in the order they were made. The
        /// game's own helpers set the links, so the behaviour matches its windows.
        /// </summary>
        public static void LinkRow(IList<Selectable> row, bool wrap = false)
        {
            if (row == null || row.Count < 2)
            {
                return;
            }

            foreach (var selectable in row)
            {
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                selectable.navigation = navigation;
            }

            for (var i = 0; i < row.Count; i++)
            {
                GuiUtils.SetNavigationLeft(row[i], i > 0 ? row[i - 1] : wrap ? row[row.Count - 1] : null);
                GuiUtils.SetNavigationRight(row[i], i < row.Count - 1 ? row[i + 1] : wrap ? row[0] : null);
            }
        }

        /// <summary>The same, up and down.</summary>
        public static void LinkColumn(IList<Selectable> column)
        {
            if (column == null || column.Count < 2)
            {
                return;
            }

            foreach (var selectable in column)
            {
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                selectable.navigation = navigation;
            }

            for (var i = 0; i + 1 < column.Count; i++)
            {
                GuiUtils.SetNavigationVertical(column[i], column[i + 1]);
            }
        }

        public static RectTransform Row(string name, Transform parent, float spacing = 8f, int padding = 0)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            Fill(layout, spacing, padding);
            return rect;
        }

        public static RectTransform Column(string name, Transform parent, float spacing = 6f, int padding = 0)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            Fill(layout, spacing, padding);
            return rect;
        }

        private static void Fill(HorizontalOrVerticalLayoutGroup layout, float spacing, int padding)
        {
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
    }

    /// <summary>
    /// The editor's text box. A box being typed in is the one widget the EventSystem really
    /// selects, so the game's own UI module sends it the pad's buttons: cross as Submit (a plain
    /// box would fire onSubmit, and Save as saved its first name and closed), circle as Cancel
    /// (the typing stopped and the old text came back before the editor saw the press, which then
    /// closed the dialog) and the D-pad as Move (the game's selection wandered off to a button the
    /// walk did not have). The editor reads the pad itself, so the box takes none of the three.
    /// Keys typed into it are not these events: Enter still submits, Esc still puts the text back.
    /// </summary>
    internal sealed class TextBox : TMP_InputField
    {
        public override void OnSubmit(BaseEventData eventData)
        {
        }

        public override void OnCancel(BaseEventData eventData)
        {
        }

        public override void OnMove(AxisEventData eventData)
        {
        }
    }

    /// <summary>
    /// The mouse wheel over a list. One notch moves a set distance and eases there, whatever the
    /// platform's wheel units are: Windows reports a notch as 1 or as 120, a trackpad sends many
    /// small steps. The distance is <see cref="Step"/> in canvas units. Anything else that moves
    /// the list (a drag, the pad's stick, a search) is taken as the new place to ease from.
    /// </summary>
    internal sealed class WheelScroll : MonoBehaviour, IScrollHandler
    {
        /// <summary>Canvas units per notch. A list that knows its row height sets a multiple of it.</summary>
        public float Step = 90f;

        private const float Ease = 18f;

        private ScrollRect _scroll;
        private float _target;
        private float _last;
        private bool _moving;

        public void Init(ScrollRect scroll)
        {
            _scroll = scroll;
        }

        /// <summary>Wheel units to notches: 120 or more is a raw Windows notch, below that it is already notches.</summary>
        private static float Notches(float y)
        {
            return Mathf.Abs(y) >= 20f ? y / 120f : y;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null)
            {
                return;
            }

            var room = Mathf.Max(0f, _scroll.content.rect.height - _scroll.viewport.rect.height);
            if (room <= 0.5f)
            {
                return;
            }

            var from = _moving ? _target : _scroll.content.anchoredPosition.y;
            _target = Mathf.Clamp(from - Notches(eventData.scrollDelta.y) * Step, 0f, room);
            _last = _scroll.content.anchoredPosition.y;
            _moving = true;
            eventData.Use();
        }

        private void Update()
        {
            if (!_moving || _scroll == null || _scroll.content == null)
            {
                return;
            }

            var at = _scroll.content.anchoredPosition;
            if (Mathf.Abs(at.y - _last) > 0.01f)
            {
                // Something else moved the list: stop easing, keep its place.
                _moving = false;
                return;
            }

            var next = Mathf.Lerp(at.y, _target, 1f - Mathf.Exp(-Ease * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - _target) < 0.5f)
            {
                next = _target;
                _moving = false;
            }

            at.y = next;
            _scroll.content.anchoredPosition = at;
            _last = next;
        }
    }
}
