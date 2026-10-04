using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Editor.View;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The small axis marker in the view's top right corner, like a 3D tool's: three coloured discs
    /// (X red, Y green, Z blue) that follow the camera, and a dim disc on each opposite end. A click
    /// on a disc looks along that axis (Right, Top, Front for the bright ones, Left, Bottom, Back for
    /// the dim ones). Under it, the name of the side the view is on and a button that switches between
    /// perspective and orthographic. The keys are 1 to 5 and the pad's L2 + D-pad (<see cref="Input.Keymap"/>).
    ///
    /// It floats over the picture, inside the room the islands leave, and slides away with the rest of
    /// the interface (Ctrl+\).
    /// </summary>
    internal static class ViewGizmo
    {
        private const float Area = 96f;
        private const float Reach = 33f;
        private const float Disc = 22f;
        private const float DimDisc = 14f;

        /// <summary>+X, -X, +Y, -Y, +Z, -Z, and the look each one clicks to.</summary>
        private static readonly Vector3[] Directions =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        private static readonly ViewPreset[] Looks =
        {
            ViewPreset.Right, ViewPreset.Left, ViewPreset.Top, ViewPreset.Bottom, ViewPreset.Front, ViewPreset.Back,
        };

        private static readonly string[] Letters = { "X", "", "Y", "", "Z", "" };

        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;
        private static readonly RectTransform[] Discs = new RectTransform[6];
        private static readonly Image[] Faces = new Image[6];
        private static readonly RectTransform[] Lines = new RectTransform[3];
        private static TextMeshProUGUI _label;
        private static Button _mode;

        /// <summary>The side the view is on, in words. For the tests.</summary>
        public static string LabelText => _label != null ? _label.text : "";

        /// <summary>The perspective switch's words. For the tests.</summary>
        public static string ModeText => _mode != null ? Kit.LabelOf(_mode).text : "";

        public static int DiscCount => Discs.Length;

        public static RectTransform Root => _root;

        /// <summary>A click on one of the six discs, exactly as the mouse does it.</summary>
        public static void Click(int index)
        {
            if (index >= 0 && index < Looks.Length)
            {
                ViewportHost.ShowView(Looks[index]);
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
            var camera = ViewportHost.Camera;
            if (_root == null || camera == null)
            {
                return;
            }

            _root.anchoredPosition = EditorWindow.UiHidden
                ? new Vector2(0f, 6000f)
                : new Vector2(-(EditorWindow.FreeRight + 4f), -(EditorWindow.FreeTop + 20f + EditorWindow.TabRoom(true)));

            var look = camera.CurrentView;
            for (var i = 0; i < Discs.Length; i++)
            {
                var dir = Directions[i];
                var across = Vector3.Dot(dir, camera.Right);
                var up = Vector3.Dot(dir, camera.Up);
                var away = Vector3.Dot(dir, camera.Forward);
                Discs[i].anchoredPosition = new Vector2(across, up) * Reach;

                // Turned away from the camera: dimmer, so what points at you reads first.
                var colour = Colour(i);
                colour.a = (i % 2 == 0 ? 1f : 0.55f) * (away > 0.2f ? 0.7f : 1f);
                Faces[i].color = colour;
                var on = look.HasValue && look.Value == Looks[i];
                Discs[i].localScale = Vector3.one * (on ? 1.18f : 1f);
            }

            for (var axis = 0; axis < Lines.Length; axis++)
            {
                var tip = Discs[axis * 2].anchoredPosition;
                var line = Lines[axis];
                line.sizeDelta = new Vector2(tip.magnitude, 2f);
                line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tip.y, tip.x) * Mathf.Rad2Deg);
            }

            var name = look.HasValue ? Name(look.Value) : "Free view";
            _label.text = name;
            Kit.SetLabel(_mode, camera.Orthographic ? "Orthographic" : "Perspective");
        }

        private static string Name(ViewPreset view)
        {
            return view == ViewPreset.Iso ? "Corner" : view.ToString();
        }

        private static Color Colour(int disc)
        {
            switch (disc / 2)
            {
                case 0:
                    return UiTheme.Dark ? new Color32(0xE5, 0x4D, 0x4D, 0xFF) : new Color32(0xD6, 0x3A, 0x3A, 0xFF);
                case 1:
                    return UiTheme.Dark ? new Color32(0x3F, 0xB9, 0x6B, 0xFF) : new Color32(0x25, 0x9C, 0x52, 0xFF);
                default:
                    return UiTheme.Dark ? new Color32(0x4C, 0x8D, 0xFF, 0xFF) : new Color32(0x0B, 0x6C, 0xF0, 0xFF);
            }
        }

        private static void Build(RectTransform host)
        {
            _root = UiBuild.Rect("ViewGizmo", host);
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.sizeDelta = new Vector2(Area + 24f, Area + 56f);

            // Sits under the islands: right after the toolbar in the draw order.
            if (EditorWindow.Toolbar != null && _root.parent == EditorWindow.Toolbar.parent)
            {
                _root.SetSiblingIndex(EditorWindow.Toolbar.GetSiblingIndex() + 1);
            }

            var centre = UiBuild.Rect("Centre", _root);
            centre.anchorMin = centre.anchorMax = new Vector2(0.5f, 1f);
            centre.pivot = new Vector2(0.5f, 0.5f);
            centre.anchoredPosition = new Vector2(0f, -Area * 0.5f);
            centre.sizeDelta = new Vector2(Area, Area);

            for (var axis = 0; axis < Lines.Length; axis++)
            {
                var line = UiBuild.Panel("Line", centre, null, Colour(axis * 2));
                line.type = Image.Type.Simple;
                line.raycastTarget = false;
                Lines[axis] = line.rectTransform;
                Lines[axis].anchorMin = Lines[axis].anchorMax = new Vector2(0.5f, 0.5f);
                Lines[axis].pivot = new Vector2(0f, 0.5f);
                Lines[axis].anchoredPosition = Vector2.zero;
            }

            // The dim ends first, so the bright ones draw over them.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = pass; i < Discs.Length; i += 2)
                {
                    var index = i;
                    var face = UiBuild.Panel("Disc" + Letters[i] + i, centre, UiTheme.Round, Colour(i));
                    face.type = UiTheme.Round != null ? Image.Type.Sliced : Image.Type.Simple;
                    var size = i % 2 == 0 ? Disc : DimDisc;
                    var rect = face.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(size, size);
                    face.gameObject.AddComponent<ClickEvents>().Clicked = _ => Click(index);
                    if (Letters[i].Length > 0)
                    {
                        var letter = Kit.Text(face.transform, Letters[i], 12f, Color.white, TextAlignmentOptions.Center);
                        letter.fontStyle = FontStyles.Bold;
                        letter.raycastTarget = false;
                        UiBuild.Stretch(letter.rectTransform);
                    }

                    Discs[i] = rect;
                    Faces[i] = face;
                }
            }

            _label = Kit.Text(_root, "", Kit.CaptionSize, UiTheme.TextOnPicture, TextAlignmentOptions.Center);
            UiBuild.OverPicture(_label);
            var label = _label.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
            label.pivot = new Vector2(0.5f, 1f);
            label.anchoredPosition = new Vector2(0f, -(Area + 2f));
            label.sizeDelta = new Vector2(Area + 24f, 16f);

            _mode = Kit.Solid(_root, "Perspective", ViewportHost.ToggleOrtho, 24f, Kit.CaptionSize);
            var mode = (RectTransform)_mode.transform;
            mode.anchorMin = mode.anchorMax = new Vector2(0.5f, 1f);
            mode.pivot = new Vector2(0.5f, 1f);
            mode.anchoredPosition = new Vector2(0f, -(Area + 22f));
            mode.sizeDelta = new Vector2(Area + 12f, 24f);
        }
    }
}
