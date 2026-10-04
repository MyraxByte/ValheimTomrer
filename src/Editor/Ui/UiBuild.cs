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
            var look = background.gameObject.AddComponent<FieldLook>();
            look.Back = background;
            look.Edge = background.GetComponent<Outline>();
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            var area = Stretch(Rect("Text Area", background.transform), 10f, 5f, 10f, 5f);

            // A little room beyond the box, or the caret at the very start of the text is cut in half.
            area.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-3f, -2f, -3f, -2f);

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
            field.caretWidth = 2;
            field.caretBlinkRate = 0.85f;
            field.selectionColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.55f);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.text = string.Empty;
            look.Field = field;
            return field;
        }

        /// <summary>A vertical list that grows with its children. Add rows to scroll.content.</summary>
        public static ScrollRect Scroll(string name, Transform parent, float spacing = 4f)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Stretch(Rect("Viewport", root));
            viewport.gameObject.AddComponent<RectMask2D>();

            // Invisible, but it takes the wheel and the drag in the gaps between rows too.
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);

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
    /// How a text box looks in each state, so it is plain which ones can be typed in: a clear outline at
    /// rest, a stronger one under the mouse, the accent while it has the keyboard, red after a value it
    /// could not take (<see cref="Flash"/>), and dim with no outline when it is switched off.
    /// </summary>
    internal sealed class FieldLook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TMP_InputField Field;
        public Image Back;
        public Outline Edge;

        /// <summary>A box that dresses itself (Quick add's search) turns this on and is left alone.</summary>
        public bool Plain;

        private bool _hover;
        private float _invalidUntil;
        private int _state = -1;
        private int _built = -1;

        public bool Invalid => Time.unscaledTime < _invalidUntil;

        /// <summary>Marks the box red for a moment: the typed value was refused.</summary>
        public void Flash()
        {
            _invalidUntil = Time.unscaledTime + 1.6f;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;

        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        private void OnDisable()
        {
            _hover = false;
        }

        private Image _caret;
        private Image _selection;

        /// <summary>
        /// The text cursor and the selected text, drawn here as well as by TextMeshPro: its own were not
        /// showing in this window, and a box you cannot see the cursor in is hard to use. Both sit in the text's
        /// own space, so they follow it when a long line scrolls. The selection is the accent, the cursor blinks.
        /// </summary>
        private void DrawCaret(bool focused)
        {
            var text = Field.textComponent;
            if (text == null)
            {
                return;
            }

            if (focused && _caret == null)
            {
                _selection = Mark(text.rectTransform, "Selection");
                _caret = Mark(text.rectTransform, "Caret");
            }

            if (_caret == null)
            {
                return;
            }

            var info = text.textInfo;
            var count = info != null ? info.characterCount : 0;
            var from = Field.stringPosition;
            var low = Mathf.Min(Field.selectionStringAnchorPosition, Field.selectionStringFocusPosition);
            var high = Mathf.Max(Field.selectionStringAnchorPosition, Field.selectionStringFocusPosition);
            var show = focused && info != null;
            _caret.gameObject.SetActive(show && (Time.unscaledTime % 1.1f) < 0.7f);
            _selection.gameObject.SetActive(show && high > low && count > 0);
            if (!show)
            {
                return;
            }

            var height = text.fontSize * 1.2f;
            var rect = text.rectTransform.rect;
            var caretAt = EdgeOf(info, from, count) - rect.xMin;
            var caretRect = _caret.rectTransform;
            caretRect.anchoredPosition = new Vector2(caretAt - 1f, 0f);
            caretRect.sizeDelta = new Vector2(2f, height);
            _caret.color = UiTheme.Text;

            if (_selection.gameObject.activeSelf)
            {
                var start = EdgeOf(info, low, count) - rect.xMin;
                var end = EdgeOf(info, high, count) - rect.xMin;
                var selectionRect = _selection.rectTransform;
                selectionRect.anchoredPosition = new Vector2(start, 0f);
                selectionRect.sizeDelta = new Vector2(Mathf.Max(2f, end - start), height);
                var accent = UiTheme.Accent;
                accent.a = 0.5f;
                _selection.color = accent;
            }
        }

        /// <summary>Where the edge before a character is, in the text's space: the start of that glyph, or the end of the last one.</summary>
        private static float EdgeOf(TMP_TextInfo info, int index, int count)
        {
            if (count == 0)
            {
                return 0f;
            }

            if (index <= 0)
            {
                return info.characterInfo[0].origin;
            }

            if (index >= count)
            {
                return info.characterInfo[count - 1].xAdvance;
            }

            return info.characterInfo[index].origin;
        }

        private static Image Mark(RectTransform parent, string name)
        {
            var image = UiBuild.Panel(name, parent, null, Color.white);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            image.gameObject.SetActive(false);
            return image;
        }

        private void Update()
        {
            if (Plain || Field == null || Back == null)
            {
                return;
            }

            DrawCaret(Field.interactable && Field.isFocused);

            // Every assignment below dirties the mesh, so only a changed state is written.
            var on = Field.interactable;
            var focused = on && Field.isFocused;
            var invalid = Invalid;
            var state = (on ? 1 : 0) | (focused ? 2 : 0) | (invalid ? 4 : 0) | (_hover ? 8 : 0);
            if (state == _state && _built == UiTheme.Generation)
            {
                return;
            }

            _state = state;
            _built = UiTheme.Generation;
            Back.color = on ? UiTheme.Field : Color.Lerp(UiTheme.Field, UiTheme.PanelFloat, 0.7f);
            if (Edge != null)
            {
                Edge.effectColor = !on ? UiTheme.Border
                    : invalid ? UiTheme.Warn
                    : focused ? UiTheme.Accent
                    : _hover ? UiTheme.FieldBorderHover
                    : UiTheme.FieldBorder;
                Edge.effectDistance = focused || invalid ? new Vector2(1.5f, -1.5f) : new Vector2(1f, -1f);
            }

            if (Field.textComponent != null)
            {
                Field.textComponent.color = on ? UiTheme.Text : UiTheme.TextDim;
            }
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
        /// <summary>The least canvas units per notch. A list that knows its row height sets a multiple of it.</summary>
        public float Step = 90f;

        /// <summary>A notch is also at least this share of the list's height, so a tall list is never a crawl.</summary>
        private const float OfView = 0.22f;

        /// <summary>Notches closer together than this (seconds) build up speed, a fast spin crosses a long list.</summary>
        private const float Chain = 0.14f;

        private const float MostBoost = 4f;
        private const float Ease = 26f;

        private float _lastNotch = -10f;
        private int _chain;

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

            var notches = Notches(eventData.scrollDelta.y);
            var now = Time.unscaledTime;
            _chain = now - _lastNotch < Chain ? _chain + 1 : 0;
            _lastNotch = now;
            var boost = Mathf.Min(MostBoost, 1f + (_chain * 0.5f));
            var step = Mathf.Max(Step, _scroll.viewport.rect.height * OfView) * boost;
            var from = _moving ? _target : _scroll.content.anchoredPosition.y;
            _target = Mathf.Clamp(from - notches * step, 0f, room);
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
