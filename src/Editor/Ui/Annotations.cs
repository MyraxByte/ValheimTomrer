using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// Lines with a small number on them, drawn over the picture: the selection's size along its edges,
    /// the gaps to the pieces beside it, the ruler. The view says where each one goes in its own units
    /// (<see cref="ToLocal"/>), between <see cref="Begin"/> and <see cref="End"/> every frame. The lines
    /// and labels are pooled, and sit under the islands so a card never has a label over it.
    /// </summary>
    internal static class Annotations
    {
        public static Color AxisX => new Color32(0xE5, 0x4D, 0x4D, 0xFF);

        public static Color AxisY => new Color32(0x3F, 0xB9, 0x6B, 0xFF);

        public static Color AxisZ => new Color32(0x4C, 0x8D, 0xFF, 0xFF);

        public static Color Gap => new Color32(0xFF, 0xB4, 0x4C, 0xFF);

        public static Color Ruler => new Color32(0xF5, 0xD0, 0x4A, 0xFF);

        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;
        private static readonly List<Item> Pool = new List<Item>();
        private static int _used;
        private static int _drawn;

        /// <summary>How many lines and dots were drawn last frame. For the tests.</summary>
        public static int Drawn => _drawn;

        /// <summary>The text of the line drawn at that place in the last frame, for the tests.</summary>
        public static string TextAt(int index)
        {
            return index >= 0 && index < _drawn && Pool[index].Label != null ? Pool[index].Label.text : "";
        }

        public static void Ensure(RectTransform host)
        {
            if (host == null || (_host == host && _root != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _host = host;
            _generation = UiTheme.Generation;
            Pool.Clear();
            _root = UiBuild.Rect("Annotations", host);
            UiBuild.Stretch(_root);

            // Over the picture, under the islands: right after the toolbar in the draw order.
            if (EditorWindow.Toolbar != null && _root.parent == EditorWindow.Toolbar.parent)
            {
                _root.SetSiblingIndex(EditorWindow.Toolbar.GetSiblingIndex() + 1);
            }
        }

        /// <summary>A screen point in the interface's own units. False before the window exists.</summary>
        public static bool ToLocal(Vector2 screen, out Vector2 local)
        {
            local = Vector2.zero;
            return _root != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
        }

        public static void Begin()
        {
            _used = 0;
        }

        /// <summary>A line from one point to another with a label on its middle.</summary>
        public static void Line(Vector2 from, Vector2 to, string text, Color colour)
        {
            var item = Next();
            if (item == null)
            {
                return;
            }

            var delta = to - from;
            item.Line.gameObject.SetActive(true);
            item.Line.color = colour;
            var rect = item.Line.rectTransform;
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            item.Pill.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text))
            {
                item.Label.text = text;
                item.Label.color = colour;
                item.Pill.rectTransform.anchoredPosition = (from + to) * 0.5f;
            }
        }

        /// <summary>A small round dot, the ends of the ruler.</summary>
        public static void Dot(Vector2 at, Color colour)
        {
            var item = Next();
            if (item == null)
            {
                return;
            }

            item.Line.gameObject.SetActive(true);
            item.Line.color = colour;
            var rect = item.Line.rectTransform;
            rect.anchoredPosition = at - new Vector2(4f, 0f);
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.localRotation = Quaternion.identity;
            item.Pill.gameObject.SetActive(false);
        }

        public static void End()
        {
            _drawn = _used;
            for (var i = _used; i < Pool.Count; i++)
            {
                Pool[i].Line.gameObject.SetActive(false);
                Pool[i].Pill.gameObject.SetActive(false);
            }
        }

        private static Item Next()
        {
            if (_root == null)
            {
                return null;
            }

            if (_used == Pool.Count)
            {
                Pool.Add(New());
            }

            return Pool[_used++];
        }

        private static Item New()
        {
            var line = UiBuild.Panel("Line", _root, null, Color.white);
            line.type = Image.Type.Simple;
            line.raycastTarget = false;
            var rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            var pill = UiBuild.Panel("Number", _root, null, UiTheme.Cap);
            pill.type = Image.Type.Simple;
            UiBuild.Rounded(pill);
            pill.raycastTarget = false;
            var shape = pill.rectTransform;
            shape.anchorMin = shape.anchorMax = new Vector2(0.5f, 0.5f);
            shape.pivot = new Vector2(0.5f, 0.5f);
            shape.sizeDelta = new Vector2(10f, 18f);
            var row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(6, 6, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            Kit.Fit(shape, true, false);
            var label = Kit.Text(pill.transform, "", 11f, UiTheme.Text, TextAlignmentOptions.Center);
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow;
            return new Item { Line = line, Pill = pill, Label = label };
        }

        private sealed class Item
        {
            public Image Line;
            public Image Pill;
            public TextMeshProUGUI Label;
        }
    }
}
