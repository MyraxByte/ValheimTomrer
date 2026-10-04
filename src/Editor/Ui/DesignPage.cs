using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Blueprints;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Input;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The Inspector's Design tab: what is selected and its size, where one piece stands (x, y, z) and
    /// how it turns, then the actions in small groups: edit (move, copy, delete, same kind), align and
    /// spread (two or more pieces), arrange (mirror, copy in a row) and view (hide, lock). Every button
    /// is reachable by the pad walk; every action has its key in the keymap too.
    /// </summary>
    internal static class DesignPage
    {
        /// <summary>Decimals in the x, y and z boxes. Millimetres, the same as the file.</summary>
        public const int PositionDecimals = 4;

        public const int YawDecimals = 2;

        private static RectTransform _root;
        private static TextMeshProUGUI _title;
        private static TextMeshProUGUI _size;
        private static TextMeshProUGUI _sub;
        private static TextMeshProUGUI _nothing;
        private static RectTransform _one;
        private static RectTransform _edit;
        private static RectTransform _align;
        private static RectTransform _arrange;
        private static TabStrip _axis;
        private static Button _step;
        private static TextMeshProUGUI _notes;
        private static readonly NumberField[] Numbers = new NumberField[4];
        private static int _version = -1;
        private static int _revision = -1;

        /// <summary>0 nothing, 1 one piece, 2 several.</summary>
        public static int Mode { get; private set; }

        public static TMP_InputField XField => Numbers[0]?.Field;

        public static TMP_InputField YField => Numbers[1]?.Field;

        public static TMP_InputField ZField => Numbers[2]?.Field;

        public static TMP_InputField YawField => Numbers[3]?.Field;

        public static string TitleText => _title != null ? _title.text : "";

        public static string SizeText => _size != null ? _size.text : "";

        public static string NotesText => _notes != null && _notes.gameObject.activeSelf ? _notes.text : "";

        public static void Show()
        {
            _version = -1;
            Refresh();
        }

        public static void Close()
        {
            _version = -1;
            _revision = -1;
        }

        public static void Tick()
        {
            if (_root == null)
            {
                return;
            }

            var revision = EditorState.Document != null ? EditorState.Document.Revision : -1;
            if (EditorState.Version != _version || revision != _revision)
            {
                Refresh();
            }
        }

        public static void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            _version = EditorState.Version;
            _revision = EditorState.Document != null ? EditorState.Document.Revision : -1;
            var pieces = EditorState.SelectedPieces();
            Mode = pieces.Count == 0 ? 0 : pieces.Count == 1 ? 1 : 2;

            _nothing.gameObject.SetActive(Mode == 0);
            _one.gameObject.SetActive(Mode == 1);
            _edit.gameObject.SetActive(Mode != 0);
            _align.gameObject.SetActive(Mode == 2);
            _arrange.gameObject.SetActive(Mode != 0);
            _axis.Set(EditorState.AlignAxis);
            Kit.SetLabel(_step, $"Step {EditorState.AngleStep:0.##}°");

            var box = EditorState.BoxOf(pieces);
            _size.text = box != null ? Size(box.Value.size) : "";
            if (Mode == 0)
            {
                _title.text = "Nothing selected";
                _sub.text = "";
                _notes.gameObject.SetActive(false);
                return;
            }

            if (Mode == 1)
            {
                var piece = pieces[0];
                var entry = PieceCatalog.Find(piece.PrefabName);
                _title.text = entry != null ? entry.DisplayName : "Unknown piece";
                _sub.text = piece.PrefabName;
                Numbers[0].Set(piece.Position.x, piece.Id);
                Numbers[1].Set(piece.Position.y, piece.Id);
                Numbers[2].Set(piece.Position.z, piece.Id);
                Numbers[3].Set(YawOf(piece.Rotation), piece.Id);
                ShowNotes(piece, entry);
                return;
            }

            _title.text = pieces.Count + " pieces";
            _sub.text = Kinds(pieces);
            _notes.gameObject.SetActive(false);
        }

        /// <summary>What is worth saying about one piece: a tilt, fields kept from the file, a scale, a problem.</summary>
        private static void ShowNotes(DocPiece piece, PieceEntry entry)
        {
            var lines = new List<string>();
            if (Vector3.Angle(piece.Rotation * Vector3.up, Vector3.up) >= 0.01f)
            {
                lines.Add("Tilted in the file. The tilt is kept; yaw turns it around the vertical axis.");
            }

            if (piece.Category.Length > 0 || piece.Rest != null)
            {
                lines.Add($"Kept from the file as it is: category {(piece.Category.Length > 0 ? piece.Category : "(empty)")}"
                    + (piece.Rest != null ? ", extra fields" : "") + ".");
            }

            var scale = Checks.ScaleOf(piece);
            if (scale != null && (Mathf.Abs(scale.Value.x - 1f) > 1e-4f || Mathf.Abs(scale.Value.y - 1f) > 1e-4f
                || Mathf.Abs(scale.Value.z - 1f) > 1e-4f))
            {
                lines.Add($"Scaled {scale.Value.x:0.###} x {scale.Value.y:0.###} x {scale.Value.z:0.###} in the file: the mod builds it at normal size.");
            }

            if (entry == null)
            {
                lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(UiTheme.Warn)}>Not a hammer piece of this game version. The game skips the whole blueprint.</color>");
            }
            else if (!entry.CanRotate)
            {
                lines.Add("This piece cannot turn in the game.");
            }

            _notes.text = string.Join("\n", lines.ToArray());
            _notes.gameObject.SetActive(lines.Count > 0);
        }

        private static string Kinds(List<DocPiece> pieces)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var piece in pieces)
            {
                if (!counts.ContainsKey(piece.PrefabName))
                {
                    counts[piece.PrefabName] = 0;
                    order.Add(piece.PrefabName);
                }

                counts[piece.PrefabName]++;
            }

            var parts = new List<string>();
            foreach (var prefab in order)
            {
                var entry = PieceCatalog.Find(prefab);
                parts.Add($"{counts[prefab]} {(entry != null ? entry.DisplayName : prefab)}");
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string Size(Vector3 size)
        {
            return $"{size.x:0.##} × {size.z:0.##} × {size.y:0.##} m";
        }

        /// <summary>Yaw in 0..360, the way the file and Tomrer show it.</summary>
        public static float YawOf(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            var degrees = Mathf.Abs(forward.x) < 1e-6f && Mathf.Abs(forward.z) < 1e-6f
                ? Mathf.Atan2(-(rotation * Vector3.right).z, (rotation * Vector3.right).x) * Mathf.Rad2Deg
                : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            return wrapped >= 359.99995f ? 0f : wrapped;
        }

        // ---------- widgets ----------

        public static void Build(RectTransform page)
        {
            var scroll = Kit.Scroll(page, 14f, 14);
            UiBuild.Stretch((RectTransform)scroll.transform);
            _root = scroll.content;

            // The selection: its name, its size, its kinds.
            var head = Kit.Column(_root, 2f, 0, "Selection");
            _title = Kit.Heading(head, "Nothing selected", out _size);
            _sub = Kit.Text(head, "", Kit.CaptionSize, UiTheme.TextDim);
            Kit.Size(_sub, -1f, 16f);
            _nothing = Kit.Note(_root, "Click a piece, drag a box round several, or press Tab to add one.");

            // One piece: where it stands and how it turns.
            _one = Kit.Column(_root, 6f, 0, "One");
            Caption(_one, "Position");
            var position = Kit.Row(_one, 6f);
            Numbers[0] = Number(position, "X", PositionDecimals, (id, v) => Move(id, 0, v));
            Numbers[1] = Number(position, "Y", PositionDecimals, (id, v) => Move(id, 1, v));
            Numbers[2] = Number(position, "Z", PositionDecimals, (id, v) => Move(id, 2, v));
            Caption(_one, "Rotation");
            var turn = Kit.Row(_one, 6f);
            Numbers[3] = Number(turn, "Yaw", YawDecimals, EditorState.SetYaw);
            Flexible(Kit.Solid(turn, "-", () => EditorState.RotateSelection(-1)), 0f, 32f);
            Flexible(Kit.Solid(turn, "+", () => EditorState.RotateSelection(1)), 0f, 32f);
            _step = Kit.Ghost(turn, "Step 22.5°", EditorCommands.CycleAngle);
            _notes = Kit.Note(_one, "");

            // Any selection: the edit actions.
            _edit = Group("Edit", out _, new[] { "Move", "Copy", "Delete", "Same kind" }, new UnityEngine.Events.UnityAction[]
            {
                () => EditorState.StartMove(),
                () => EditorState.StartDuplicate(),
                EditorState.DeleteSelection,
                EditorState.SelectSimilar,
            });

            // Two or more: line them up or spread them, along one axis.
            _align = Kit.Column(_root, 6f, 0, "Align");
            Caption(_align, "Align and spread");
            var axisRow = Kit.Row(_align, 6f);
            _axis = Kit.Segmented(axisRow, new[] { "X", "Y", "Z" }, SetAxis);
            Kit.Spring(axisRow);
            Buttons(_align, new[] { "Low", "Middle", "High", "Spread" }, new UnityEngine.Events.UnityAction[]
            {
                () => EditorState.AlignSelection(-1),
                () => EditorState.AlignSelection(0),
                () => EditorState.AlignSelection(1),
                () => EditorState.SpreadSelection(),
            });

            // Arrange and view.
            _arrange = Kit.Column(_root, 6f, 0, "Arrange");
            Caption(_arrange, "Arrange");
            Buttons(_arrange, new[] { "Mirror X", "Mirror Z", "Row copy" }, new UnityEngine.Events.UnityAction[]
            {
                () => EditorState.MirrorSelection(0),
                () => EditorState.MirrorSelection(2),
                () => EditorState.CopyInRow(Bindings.CameraRight()),
            });
            Caption(_arrange, "View");
            Buttons(_arrange, new[] { "Hide", "Lock", "Isolate", "Show all" }, new UnityEngine.Events.UnityAction[]
            {
                EditorState.HideSelection,
                EditorState.LockSelection,
                EditorState.IsolateSelection,
                EditorState.ShowAll,
            });
            Buttons(_arrange, new[] { "Look at it", "Top", "Corner" }, new UnityEngine.Events.UnityAction[]
            {
                ViewportHost.Frame,
                () => ViewportHost.ShowView(ValheimTomrer.Editor.View.ViewPreset.Top),
                () => ViewportHost.ShowView(ValheimTomrer.Editor.View.ViewPreset.Iso),
            });

            Refresh();
        }

        private static void SetAxis(int axis)
        {
            while (EditorState.AlignAxis != axis)
            {
                EditorState.CycleAlignAxis();
            }
        }

        private static RectTransform Group(string name, out TextMeshProUGUI caption, string[] labels, UnityEngine.Events.UnityAction[] actions)
        {
            var column = Kit.Column(_root, 6f, 0, name);
            caption = Caption(column, name);
            Buttons(column, labels, actions);
            return column;
        }

        private static TextMeshProUGUI Caption(Transform parent, string text)
        {
            var label = Kit.Text(parent, text, Kit.CaptionSize, UiTheme.TextDim);
            Kit.Size(label, -1f, 16f);
            return label;
        }

        /// <summary>A row of buttons sharing the width.</summary>
        private static void Buttons(Transform parent, string[] labels, UnityEngine.Events.UnityAction[] actions)
        {
            var row = Kit.Row(parent, 6f);
            for (var i = 0; i < labels.Length; i++)
            {
                Flexible(Kit.Solid(row, labels[i], actions[i]), 1f, 0f);
            }
        }

        private static void Flexible(Button button, float flexible, float width)
        {
            var element = button.GetComponent<LayoutElement>();
            element.flexibleWidth = flexible;
            element.minWidth = width > 0f ? width : 20f;
            element.preferredWidth = width > 0f ? width : 20f;

            // A button that shares the row with others gets what is left: the words shrink a little to fit.
            var label = Kit.LabelOf(button);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = Kit.BodySize;
            UiBuild.Stretch(label.rectTransform, 2f, 0f, 2f, 0f);
        }

        private static NumberField Number(Transform row, string caption, int decimals, Action<int, float> commit)
        {
            var field = Kit.Field(row, caption, "", caption.Length > 1 ? 26f : 10f);
            Kit.Size(field, -1f, Kit.ControlHeight, 1f);
            field.contentType = TMP_InputField.ContentType.Standard;
            var number = new NumberField { Field = field, Decimals = decimals, Commit = commit, Look = field.GetComponent<FieldLook>() };
            field.onEndEdit.AddListener(number.Finish);
            return number;
        }

        /// <summary>The x, y or z box moved one piece.</summary>
        private static void Move(int id, int axis, float value)
        {
            var piece = EditorState.Document != null ? EditorState.Document.Find(id) : null;
            if (piece == null)
            {
                return;
            }

            var position = piece.Position;
            position[axis] = value;
            EditorState.SetPosition(id, position);
        }

        /// <summary>A number box: shows the piece's value, and on Enter or leaving it, writes what was typed.</summary>
        private sealed class NumberField
        {
            public TMP_InputField Field;
            public int Decimals;
            public Action<int, float> Commit;
            public FieldLook Look;

            private string _shown = "";
            private int _id = -1;

            public void Set(float value, int id)
            {
                _id = id;
                _shown = BlueprintFormat.FormatNumber(value, Decimals);
                if (!Field.isFocused)
                {
                    Field.SetTextWithoutNotify(_shown);
                }
            }

            public void Finish(string text)
            {
                if (Field.wasCanceled || _id < 0)
                {
                    Field.SetTextWithoutNotify(_shown);
                    return;
                }

                // Old files and some keyboards write the decimal mark as a comma.
                var typed = (text ?? "").Trim().Replace(',', '.');
                if (typed.Length > 0
                    && float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    && !float.IsNaN(value) && !float.IsInfinity(value)
                    && BlueprintFormat.FormatNumber(value, Decimals) != _shown)
                {
                    Commit(_id, value);
                    return;
                }

                // Something typed that is not a number: the box goes red for a moment and shows the old value.
                if (typed.Length > 0 && Look != null && !float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    Look.Flash();
                }

                Field.SetTextWithoutNotify(_shown);
            }
        }
    }
}
