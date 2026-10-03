using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The left panel: every piece of the open blueprint, grouped by kind (walls, roof, floors...), the
    /// way a design tool lists layers. A group folds with a click on its heading; a double click selects
    /// the whole group. A row selects its piece (Shift adds or takes out, a double click selects the
    /// same kind); Hide and Lock show under the mouse, and stay shown while they are on.
    ///
    /// The list is virtual: only the rows in view exist, so a blueprint of a thousand pieces costs no
    /// more than one of twenty. Rows are plain images, never Selectables, so the pad walk leaves them
    /// to the mouse; the pad selects with the crosshair, and hides and locks from the Inspector.
    /// </summary>
    internal static class LayersPanel
    {
        private const float RowHeight = 28f;
        private const float HeadHeight = 44f;
        private const float SearchHeight = 40f;
        private const float DoubleClick = 0.35f;

        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;
        private static TextMeshProUGUI _count;
        private static TMP_InputField _search;
        private static ScrollRect _scroll;
        private static RectTransform _content;
        private static TextMeshProUGUI _empty;

        private static readonly List<Line> Lines = new List<Line>();
        private static readonly List<RowView> Pool = new List<RowView>();
        private static readonly HashSet<string> Folded = new HashSet<string>();

        private static BlueprintDocument _document;
        private static int _revision = -1;
        private static int _version = -1;
        private static string _filter = "";
        private static int _first = -1;
        private static float _lastHeight = -1f;
        private static int _lastClicked = -1;
        private static float _lastClickAt;

        /// <summary>A row was clicked: the piece, and true when Shift was held. The session selects.</summary>
        public static Action<int, bool> PieceClicked;

        /// <summary>Rows of pieces in the list now (folded groups leave theirs out).</summary>
        public static int RowCount => Lines.Count(l => l.Piece != null);

        public static int GroupCount => Lines.Count(l => l.Piece == null);

        /// <summary>Rows that exist as objects. Only the ones in view.</summary>
        public static int LiveRows => Pool.Count;

        public static string CountText => _count != null ? _count.text : "";

        /// <summary>What the search box says now.</summary>
        public static string Search => _search != null ? _search.text : _filter;

        public static void Ensure(RectTransform host)
        {
            if (host == null || (_host == host && _root != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _host = host;
            _generation = UiTheme.Generation;
            Pool.Clear();
            _first = -1;
            Build(host);
        }

        public static void Show(BlueprintDocument document)
        {
            _document = document;
            _revision = -1;
            Reload();
        }

        public static void Close()
        {
            _document = null;
            Lines.Clear();
        }

        public static void Tick()
        {
            if (_root == null)
            {
                return;
            }

            if (_document != null && (_document.Revision != _revision || EditorState.Version != _version))
            {
                Reload();
                return;
            }

            var height = _scroll.viewport.rect.height;
            if (!Mathf.Approximately(height, _lastHeight))
            {
                _lastHeight = height;
                _first = -1;
                Fill();
            }
        }

        /// <summary>Folds a group in or out by its name ("Wall", "Roof"...). For the tests too.</summary>
        public static void ToggleGroup(string group)
        {
            if (!Folded.Remove(group))
            {
                Folded.Add(group);
            }

            Reload();
        }

        /// <summary>Filters the rows by a piece's name. Empty shows all.</summary>
        public static void SetSearch(string text)
        {
            if (_search != null && _search.text != text)
            {
                _search.text = text ?? "";   // fires onValueChanged
                return;
            }

            _filter = (text ?? "").Trim().ToLowerInvariant();
            Reload();
        }

        private static void Reload()
        {
            Lines.Clear();
            _version = EditorState.Version;
            if (_document != null)
            {
                _revision = _document.Revision;
                var groups = new Dictionary<string, List<DocPiece>>();
                var order = new List<string>();
                foreach (var piece in _document.Pieces)
                {
                    var entry = PieceCatalog.Find(piece.PrefabName);
                    var name = entry != null ? entry.DisplayName : piece.PrefabName;
                    if (_filter.Length > 0 && name.ToLowerInvariant().IndexOf(_filter, StringComparison.Ordinal) < 0
                        && piece.PrefabName.ToLowerInvariant().IndexOf(_filter, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    var group = GroupOf(entry);
                    if (!groups.TryGetValue(group, out var list))
                    {
                        list = new List<DocPiece>();
                        groups[group] = list;
                        order.Add(group);
                    }

                    list.Add(piece);
                }

                foreach (var group in order)
                {
                    var pieces = groups[group];
                    var selected = pieces.Count(p => EditorState.IsSelected(p.Id));
                    Lines.Add(new Line { Group = group, Count = pieces.Count, Selected = selected, Ids = pieces.Select(p => p.Id).ToArray() });
                    if (Folded.Contains(group))
                    {
                        continue;
                    }

                    foreach (var piece in pieces)
                    {
                        Lines.Add(new Line { Group = group, Piece = piece });
                    }
                }
            }

            _count.text = _document == null ? "" : _document.Pieces.Count == 1 ? "1 piece" : _document.Pieces.Count + " pieces";
            _empty.gameObject.SetActive(_document != null && Lines.Count == 0);
            _empty.text = _filter.Length > 0 ? "Nothing matches." : "No piece yet. Press Tab to add one.";
            _content.sizeDelta = new Vector2(0f, Lines.Count * RowHeight + 8f);
            _first = -1;
            Fill();
        }

        /// <summary>The group a piece belongs to: its first building tag ("Wall", "Roof"), else "Other".</summary>
        private static string GroupOf(PieceEntry entry)
        {
            if (entry == null || entry.UsageTags == null || entry.UsageTags.Length == 0)
            {
                return "Other";
            }

            // The broad "Building" tag says little; a more specific one wins.
            foreach (var tag in entry.UsageTags)
            {
                if (tag != "Building" && tag != "Misc")
                {
                    return tag;
                }
            }

            return entry.UsageTags[0];
        }

        private static void Fill()
        {
            if (_scroll == null || _scroll.viewport == null)
            {
                return;
            }

            var offset = Mathf.Max(0f, _content.anchoredPosition.y);
            var first = Mathf.Max(0, Mathf.FloorToInt(offset / RowHeight));
            var need = Mathf.Min(Mathf.CeilToInt(_scroll.viewport.rect.height / RowHeight) + 2, Mathf.Max(0, Lines.Count - first));
            while (Pool.Count < need)
            {
                Pool.Add(NewRow());
            }

            _first = first;
            for (var i = 0; i < Pool.Count; i++)
            {
                var index = first + i;
                if (i >= need || index >= Lines.Count)
                {
                    Pool[i].Clear();
                    continue;
                }

                Pool[i].Show(Lines[index], -index * RowHeight - 4f);
            }
        }

        private static void Clicked(RowView row, PointerEventData data)
        {
            var line = row.Line;
            if (line == null || data.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var now = Time.unscaledTime;
            var key = line.Piece != null ? line.Piece.Id : -1000 - line.Group.GetHashCode();
            var twice = key == _lastClicked && now - _lastClickAt < DoubleClick;
            _lastClicked = twice ? int.MinValue : key;
            _lastClickAt = now;

            if (line.Piece == null)
            {
                if (twice)
                {
                    // The first click folded it: put it back, and select the whole group.
                    ToggleGroup(line.Group);
                    EditorState.Select(line.Ids);
                    return;
                }

                ToggleGroup(line.Group);
                return;
            }

            if (twice)
            {
                EditorState.Select(line.Piece.Id);
                EditorState.SelectSimilar();
                return;
            }

            var shift = ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false);
            PieceClicked?.Invoke(line.Piece.Id, shift);
        }

        // ---------- widgets ----------

        private static void Build(RectTransform host)
        {
            _root = UiBuild.Rect("Layers", host);
            UiBuild.Stretch(_root);

            var head = UiBuild.Rect("Head", _root);
            Top(head, 0f, HeadHeight);
            var title = Kit.Text(head, "Layers", Kit.BodySize, UiTheme.Text);
            title.fontStyle = FontStyles.Bold;
            UiBuild.Stretch(title.rectTransform, 14f, 0f, 14f, 0f);
            _count = Kit.Text(head, "", Kit.CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            UiBuild.Stretch(_count.rectTransform, 14f, 0f, 14f, 0f);

            _search = Kit.Field(_root, "", "Find a piece in this blueprint");
            var search = (RectTransform)_search.transform;
            Top(search, HeadHeight, Kit.ControlHeight);
            search.offsetMin = new Vector2(12f, search.offsetMin.y);
            search.offsetMax = new Vector2(-12f, search.offsetMax.y);
            _search.onValueChanged.AddListener(text =>
            {
                _filter = (text ?? "").Trim().ToLowerInvariant();
                Reload();
            });

            _scroll = UiBuild.Scroll("Rows", _root);
            var rows = (RectTransform)_scroll.transform;
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.offsetMin = new Vector2(6f, 6f);
            rows.offsetMax = new Vector2(-6f, -(HeadHeight + SearchHeight));
            _scroll.GetComponent<WheelScroll>().Step = 3f * RowHeight;
            _content = _scroll.content;
            UnityEngine.Object.DestroyImmediate(_content.GetComponent<VerticalLayoutGroup>());
            UnityEngine.Object.DestroyImmediate(_content.GetComponent<ContentSizeFitter>());
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _scroll.onValueChanged.AddListener(_ => Fill());

            _empty = Kit.Note(_scroll.viewport, "", Kit.CaptionSize, UiTheme.TextDim);
            _empty.alignment = TextAlignmentOptions.Top;
            UiBuild.Stretch(_empty.rectTransform, 12f, 0f, 12f, 16f);
        }

        private static void Top(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static RowView NewRow()
        {
            var back = UiBuild.Panel("Row", _content, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            var rect = back.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight - 2f);

            var icon = UiBuild.Panel("Icon", rect, null);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(18f, 18f);

            var name = Kit.Text(rect, "", Kit.BodySize, UiTheme.Text);
            UiBuild.Stretch(name.rectTransform, 46f, 0f, 92f, 0f);

            // The fold mark of a group heading: a chevron turned down while the group is open.
            var fold = Kit.Text(rect, "›", 15f, UiTheme.TextDim, TextAlignmentOptions.Center);
            fold.rectTransform.anchorMin = fold.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fold.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fold.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            fold.rectTransform.sizeDelta = new Vector2(14f, 18f);

            var view = new RowView { Rect = rect, Back = back, Icon = icon, Name = name, Fold = fold };
            view.Hide = Toggle(rect, "Hide", 48f, view, true);
            view.Lock = Toggle(rect, "Lock", 6f, view, false);
            back.gameObject.AddComponent<ClickEvents>().Clicked = data => Clicked(view, data);
            back.gameObject.AddComponent<HoverEvents>().Changed = on =>
            {
                view.Hovered = on;
                view.Refresh();
            };
            return view;
        }

        /// <summary>A small Hide or Lock word at the end of a row: an image with a click handler, never a Selectable.</summary>
        private static TextMeshProUGUI Toggle(RectTransform row, string text, float fromRight, RowView view, bool hide)
        {
            var label = Kit.Text(row, text, 11f, UiTheme.TextDim, TextAlignmentOptions.Center);
            label.raycastTarget = true;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-fromRight, 0f);
            rect.sizeDelta = new Vector2(40f, RowHeight - 6f);
            label.gameObject.AddComponent<ClickEvents>().Clicked = data =>
            {
                var piece = view.Line != null ? view.Line.Piece : null;
                var ids = piece != null ? new[] { piece.Id } : view.Line != null ? view.Line.Ids : null;
                if (ids == null || ids.Length == 0)
                {
                    return;
                }

                if (hide)
                {
                    EditorState.SetHidden(ids, !ids.All(EditorState.IsHidden));
                }
                else
                {
                    EditorState.SetLocked(ids, !ids.All(EditorState.IsLocked));
                }
            };
            return label;
        }

        /// <summary>One line of the list: a group heading (Piece null) or a piece.</summary>
        private sealed class Line
        {
            public string Group;
            public DocPiece Piece;
            public int Count;
            public int Selected;
            public int[] Ids;
        }

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Back;
            public Image Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Fold;
            public TextMeshProUGUI Hide;
            public TextMeshProUGUI Lock;
            public Line Line;
            public bool Hovered;

            public void Show(Line line, float top)
            {
                Line = line;
                Rect.anchoredPosition = new Vector2(0f, top);
                Rect.gameObject.SetActive(true);
                if (line.Piece == null)
                {
                    Icon.enabled = false;
                    Fold.gameObject.SetActive(true);
                    Fold.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Folded.Contains(line.Group) ? 0f : -90f);
                    Name.text = $"{Plural(line.Group)}  <color=#{ColorUtility.ToHtmlStringRGB(UiTheme.TextDim)}>{line.Count}</color>";
                    Name.fontStyle = FontStyles.Bold;
                    Name.rectTransform.offsetMin = new Vector2(26f, 0f);
                }
                else
                {
                    var entry = PieceCatalog.Find(line.Piece.PrefabName);
                    Icon.sprite = entry != null ? entry.Icon : null;
                    Icon.enabled = Icon.sprite != null;
                    Fold.gameObject.SetActive(false);
                    Name.text = entry != null ? entry.DisplayName : line.Piece.PrefabName;
                    Name.fontStyle = FontStyles.Normal;
                    Name.rectTransform.offsetMin = new Vector2(46f, 0f);
                }

                Refresh();
            }

            public void Refresh()
            {
                if (Line == null)
                {
                    return;
                }

                var piece = Line.Piece;
                var selected = piece != null ? EditorState.IsSelected(piece.Id) : Line.Count > 0 && Line.Selected == Line.Count;
                var hidden = piece != null ? EditorState.IsHidden(piece.Id) : Line.Ids.Length > 0 && Line.Ids.All(EditorState.IsHidden);
                var locked = piece != null ? EditorState.IsLocked(piece.Id) : Line.Ids.Length > 0 && Line.Ids.All(EditorState.IsLocked);

                var fill = selected ? UiTheme.Accent : UiTheme.SurfaceHover;
                fill.a = selected ? 0.22f : Hovered ? 1f : 0f;
                Back.color = fill;
                Name.color = hidden ? UiTheme.TextDim : UiTheme.Text;

                Hide.text = hidden ? "Show" : "Hide";
                Lock.text = locked ? "Locked" : "Lock";
                Hide.color = hidden ? UiTheme.Accent : UiTheme.TextDim;
                Lock.color = locked ? UiTheme.Accent : UiTheme.TextDim;
                Hide.gameObject.SetActive(Hovered || hidden);
                Lock.gameObject.SetActive(Hovered || locked);
            }

            public void Clear()
            {
                Line = null;
                Hovered = false;
                Rect.gameObject.SetActive(false);
            }

            private static string Plural(string group)
            {
                if (string.IsNullOrEmpty(group))
                {
                    return "Other";
                }

                // The game's tags are singular ("Wall", "Door"); a few already are plural.
                return group.EndsWith("s") ? group : group + "s";
            }
        }
    }
}
