using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's widget kit: the few building blocks every panel is made of, so the whole window
    /// shares one look. Text, ghost buttons (no fill until the mouse is on them), solid and primary
    /// buttons, rows and columns, dividers, inline fields with their caption inside, tab strips and
    /// segmented switches. Every colour comes from <see cref="UiTheme"/>.
    /// </summary>
    internal static class Kit
    {
        /// <summary>The height of a control: a button, a field, a list row.</summary>
        public const float ControlHeight = 28f;

        /// <summary>The body text size. Captions are 12, headings 13 semibold.</summary>
        public const float BodySize = 13f;

        public const float CaptionSize = 12f;

        public static TextMeshProUGUI Text(
            Transform parent, string text, float size = BodySize, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var label = UiBuild.Label("Text", parent, text, size, align, color ?? UiTheme.Text);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>A text that wraps, for notes and messages.</summary>
        public static TextMeshProUGUI Note(Transform parent, string text, float size = CaptionSize, Color? color = null)
        {
            var label = UiBuild.Label("Note", parent, text, size, TextAlignmentOptions.TopLeft, color ?? UiTheme.TextDim);
            label.enableWordWrapping = true;
            return label;
        }

        /// <summary>Gives a widget a fixed or a flexible size inside a layout group. A negative value leaves that one alone.</summary>
        public static LayoutElement Size(Component widget, float width = -1f, float height = -1f, float flexible = -1f)
        {
            var element = widget.GetComponent<LayoutElement>() ?? widget.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                element.minWidth = element.preferredWidth = width;
            }

            if (height >= 0f)
            {
                element.minHeight = element.preferredHeight = height;
            }

            if (flexible >= 0f)
            {
                element.flexibleWidth = flexible;
            }

            return element;
        }

        /// <summary>A row: children side by side, as tall as the row.</summary>
        public static RectTransform Row(Transform parent, float spacing = 6f, float height = ControlHeight, string name = "Row")
        {
            var rect = UiBuild.Rect(name, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            if (height > 0f)
            {
                Size(rect, -1f, height);
            }

            return rect;
        }

        /// <summary>A column: children stacked, as wide as the column.</summary>
        public static RectTransform Column(Transform parent, float spacing = 8f, int padding = 0, string name = "Column")
        {
            var rect = UiBuild.Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>Lets a column or a row take the size its children want.</summary>
        public static void Fit(RectTransform rect, bool horizontal, bool vertical)
        {
            var fitter = rect.gameObject.GetComponent<ContentSizeFitter>() ?? rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>A one-pixel line: across a column, or down a row.</summary>
        public static Image Divider(Transform parent, bool vertical = false)
        {
            var line = UiBuild.Panel("Divider", parent, null, UiTheme.Border);
            line.type = Image.Type.Simple;
            line.raycastTarget = false;
            if (vertical)
            {
                Size(line, 1f, 18f);
            }
            else
            {
                Size(line, -1f, 1f);
            }

            return line;
        }

        /// <summary>Flexible empty space in a row or a column.</summary>
        public static RectTransform Spring(Transform parent)
        {
            var rect = UiBuild.Rect("Spring", parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.flexibleHeight = 1f;
            return rect;
        }

        /// <summary>The colours of a ghost button: nothing at rest, the hover fill under the mouse.</summary>
        public static ColorBlock GhostColours()
        {
            var colours = ColorBlock.defaultColorBlock;
            var none = UiTheme.SurfaceHover;
            none.a = 0f;
            colours.normalColor = none;
            colours.highlightedColor = UiTheme.SurfaceHover;
            colours.selectedColor = UiTheme.SurfaceHover;
            colours.pressedColor = UiTheme.SurfacePressed;
            colours.disabledColor = none;
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.06f;
            return colours;
        }

        /// <summary>A button that is only text until the mouse is on it. The default for toolbars and menus.</summary>
        public static Button Ghost(Transform parent, string text, UnityAction click, float height = ControlHeight, float size = BodySize)
        {
            var button = Base(parent, text, click, height, size);
            button.colors = GhostColours();
            return button;
        }

        /// <summary>A button with a surface fill: actions inside a panel.</summary>
        public static Button Solid(Transform parent, string text, UnityAction click, float height = ControlHeight, float size = BodySize)
        {
            var button = Base(parent, text, click, height, size);
            button.colors = UiBuild.ButtonColours();
            return button;
        }

        /// <summary>The one button that matters most on screen: accent fill, white text.</summary>
        public static Button Primary(Transform parent, string text, UnityAction click, float height = ControlHeight)
        {
            var button = Base(parent, text, click, height, BodySize);
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = UiTheme.Accent;
            colours.highlightedColor = Color.Lerp(UiTheme.Accent, Color.white, 0.12f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = Color.Lerp(UiTheme.Accent, Color.black, 0.15f);
            var off = UiTheme.Accent;
            off.a = 0.35f;
            colours.disabledColor = off;
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.06f;
            button.colors = colours;
            LabelOf(button).color = UiTheme.TextOnAccent;
            LabelOf(button).fontStyle = FontStyles.Bold;
            return button;
        }

        private static Button Base(Transform parent, string text, UnityAction click, float height, float size)
        {
            var image = UiBuild.Panel(text, parent, null, Color.white);
            image.type = Image.Type.Simple;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            if (click != null)
            {
                button.onClick.AddListener(click);
            }

            var label = Text(image.transform, text, size, UiTheme.Text, TextAlignmentOptions.Center);
            UiBuild.Stretch(label.rectTransform, 8f, 0f, 8f, 0f);

            // Measured, not guessed: the font is the game's and the words differ in length a lot.
            var wide = label.GetPreferredValues(text, 4000f, 0f).x;
            Size(image, Mathf.Max(height, (wide > 1f ? wide : text.Length * 7f) + 18f), height);
            return button;
        }

        public static TextMeshProUGUI LabelOf(Button button)
        {
            return button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
        }

        /// <summary>Sets a button's words and keeps its width fitting them.</summary>
        public static void SetLabel(Button button, string text)
        {
            var label = LabelOf(button);
            if (label == null || label.text == text)
            {
                return;
            }

            label.text = text;
            var element = button.GetComponent<LayoutElement>();
            if (element != null)
            {
                var wide = label.GetPreferredValues(text, 4000f, 0f).x;
                element.minWidth = element.preferredWidth = Mathf.Max(element.minHeight, wide + 18f);
            }
        }

        /// <summary>Takes a button out of the pad walk's own navigation: the walk steps by screen position.</summary>
        public static void NoNavigation(Selectable selectable)
        {
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        /// <summary>
        /// A text box with its caption inside, on the left, like a design tool's number fields
        /// ("X 2.000"). The caption is dim and the text starts after it.
        /// </summary>
        public static TMP_InputField Field(Transform parent, string caption, string placeholder, float captionWidth = 0f)
        {
            var field = UiBuild.InputField(caption + "Field", parent, placeholder, ControlHeight);
            field.textComponent.fontSize = BodySize;
            ((TMP_Text)field.placeholder).fontSize = BodySize;
            field.pointSize = BodySize;
            if (!string.IsNullOrEmpty(caption))
            {
                var width = captionWidth > 0f ? captionWidth : 14f + (caption.Length * 7f);
                var tag = Text(field.transform, caption, CaptionSize, UiTheme.TextDim);
                tag.rectTransform.anchorMin = new Vector2(0f, 0f);
                tag.rectTransform.anchorMax = new Vector2(0f, 1f);
                tag.rectTransform.pivot = new Vector2(0f, 0.5f);
                tag.rectTransform.offsetMin = new Vector2(8f, 0f);
                tag.rectTransform.offsetMax = new Vector2(8f + width, 0f);
                field.textViewport.offsetMin = new Vector2(10f + width, field.textViewport.offsetMin.y);
            }

            return field;
        }

        /// <summary>A section heading: a small semibold title, and an optional dim note on the right.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string title, out TextMeshProUGUI note)
        {
            var row = Row(parent, 6f, 20f, "Heading");
            var label = Text(row, title, BodySize, UiTheme.Text);
            label.fontStyle = FontStyles.Bold;
            Size(label, -1f, -1f, 1f);
            note = Text(row, "", CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            Size(note, -1f, -1f, 1f);
            return label;
        }

        /// <summary>Tabs: words in a row, the open one bright with an accent line under it.</summary>
        public static TabStrip Tabs(Transform parent, string[] names, Action<int> pick)
        {
            var row = Row(parent, 16f, 36f, "Tabs");
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(14, 14, 0, 0);
            var strip = new TabStrip { Root = row };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var button = Ghost(row, names[i], () => pick(index), 36f);
                button.colors = NoFill();
                var label = LabelOf(button);
                label.fontStyle = FontStyles.Bold;
                UiBuild.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
                Size(button, label.GetPreferredValues(names[i], 4000f, 0f).x + 2f, 36f);
                var line = UiBuild.Panel("Line", button.transform, null, UiTheme.Accent);
                line.type = Image.Type.Simple;
                line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(1f, 0f);
                line.rectTransform.pivot = new Vector2(0.5f, 0f);
                line.rectTransform.sizeDelta = new Vector2(0f, 2f);
                line.rectTransform.anchoredPosition = Vector2.zero;
                strip.Buttons.Add(button);
                strip.Labels.Add(label);
                strip.Lines.Add(line);
            }

            return strip;
        }

        /// <summary>A switch of a few words, one on: the modes in the header, the align axis.</summary>
        public static TabStrip Segmented(Transform parent, string[] names, Action<int> pick, float height = ControlHeight)
        {
            var back = UiBuild.Panel("Segmented", parent, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            var layout = back.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            Fit(back.rectTransform, true, false);
            Size(back, -1f, height);
            var strip = new TabStrip { Root = back.rectTransform, Segmented = true };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var button = Ghost(back.transform, names[i], () => pick(index), height - 4f);
                strip.Buttons.Add(button);
                strip.Labels.Add(LabelOf(button));
                strip.Fills.Add((Image)button.targetGraphic);
            }

            return strip;
        }

        private static ColorBlock NoFill()
        {
            var colours = ColorBlock.defaultColorBlock;
            var none = new Color(1f, 1f, 1f, 0f);
            colours.normalColor = colours.highlightedColor = colours.selectedColor = colours.pressedColor = colours.disabledColor = none;
            return colours;
        }

        /// <summary>A small pill of text: a count, a state.</summary>
        public static TextMeshProUGUI Chip(Transform parent, string text, out Image back)
        {
            back = UiBuild.Panel("Chip", parent, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            back.raycastTarget = false;
            var label = Text(back.transform, text, 11f, UiTheme.TextDim, TextAlignmentOptions.Center);
            UiBuild.Stretch(label.rectTransform, 6f, 0f, 6f, 0f);
            Size(back, label.GetPreferredValues(text, 4000f, 0f).x + 14f, 18f);
            return label;
        }

        /// <summary>A vertical list that scrolls with the wheel, the pad's right stick and a drag.</summary>
        public static ScrollRect Scroll(Transform parent, float spacing = 8f, int padding = 14)
        {
            var scroll = UiBuild.Scroll("Scroll", parent, spacing);
            scroll.content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
            return scroll;
        }
    }

    /// <summary>A row of tabs or a segmented switch: which one is on, drawn.</summary>
    internal sealed class TabStrip
    {
        public RectTransform Root;
        public bool Segmented;
        public readonly List<Button> Buttons = new List<Button>();
        public readonly List<TextMeshProUGUI> Labels = new List<TextMeshProUGUI>();
        public readonly List<Image> Lines = new List<Image>();
        public readonly List<Image> Fills = new List<Image>();

        public int Current { get; private set; } = -1;

        public void Set(int index)
        {
            Current = index;
            for (var i = 0; i < Labels.Count; i++)
            {
                var on = i == index;
                Labels[i].color = on ? UiTheme.Text : UiTheme.TextDim;
                if (i < Lines.Count)
                {
                    Lines[i].enabled = on;
                }

                if (i < Fills.Count)
                {
                    // The segment that is on keeps a fill of its own; a tint on top of the hover colours.
                    var colours = Kit.GhostColours();
                    if (on)
                    {
                        colours.normalColor = colours.selectedColor = colours.highlightedColor = UiTheme.SurfacePressed;
                    }

                    Buttons[i].colors = colours;
                }
            }
        }
    }

    /// <summary>Calls back when the mouse comes onto a widget and leaves it. Hover-only details, such as a row's Hide and Lock.</summary>
    internal sealed class HoverEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Changed;

        public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);
    }

    /// <summary>A click with the button that made it: left and right do different things on rows and tiles.</summary>
    internal sealed class ClickEvents : MonoBehaviour, IPointerClickHandler
    {
        public Action<PointerEventData> Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(eventData);
    }
}
