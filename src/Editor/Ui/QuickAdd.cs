using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Input;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// Quick add: the one way to pick a piece to place, by keyboard, mouse or pad. Tab (or the pad's
    /// cross, or the Add mode in the header) opens a search box over the view. Typing filters at once,
    /// the first match is lit, Enter places it; the arrow keys move the light, a click places the piece
    /// under the mouse, a right click stars it. Above the grid: All, Recent (newest first), Starred, then
    /// the game's building tags. On the pad the D-pad moves the light, L1 and R1 change the tag, cross
    /// places and circle closes.
    ///
    /// Every tile is built once per catalog and filtered by switching tiles on and off, so typing costs
    /// nothing. The popup is switched off while closed; it is built and filled only while open, because
    /// TextMeshProUGUI measures nothing in an inactive object.
    /// </summary>
    internal static class QuickAdd
    {
        public const string RecentKey = "@recent";
        public const string FavouriteKey = "@favourite";

        /// <summary>The tile width the grid aims for; the column count follows the card's width.</summary>
        private const float TileTarget = 84f;
        private const float SideWidth = 176f;
        private const float Spacing = 8f;
        private const float SearchHeight = 46f;
        private const float ChipHeight = 30f;
        private const float FootHeight = 30f;

        private static GameObject _host;
        private static RectTransform _card;
        private static TMP_InputField _search;
        private static RectTransform _chipRow;
        private static ScrollRect _scroll;
        private static GridLayoutGroup _grid;
        private static TextMeshProUGUI _info;
        private static TextMeshProUGUI _count;
        private static TextMeshProUGUI _empty;
        private static int _generation = -1;
        private static int _catalogGeneration = -1;
        private static int _catalogCount = -1;

        private static int Columns = 7;
        private static readonly List<Tile> Tiles = new List<Tile>();
        private static readonly List<Tile> Shown = new List<Tile>();
        private static readonly List<Chip> Chips = new List<Chip>();
        private static string _tag;
        private static int _lit;

        /// <summary>A piece was picked: the session puts it in hand.</summary>
        public static Action<PieceEntry> PieceChosen;

        public static bool IsOpen => _host != null && _host.activeSelf;

        public static int ShownCount => Shown.Count;

        public static int TotalCount => Tiles.Count;

        public static string Search => _search != null ? _search.text : "";

        public static string Tag => _tag;

        /// <summary>The lit tile's piece: what Enter, a click with the pad's cross, places.</summary>
        public static PieceEntry Lit => _lit >= 0 && _lit < Shown.Count ? Shown[_lit].Entry : null;

        public static int LitIndex => _lit;

        /// <summary>The tag chips in order: null (All), Recent, Starred, then the game's tags.</summary>
        public static IReadOnlyList<string> TagKeys => Chips.Select(c => c.Key).ToList();

        /// <summary>Builds the popup if the window was built again. Called by the session when the editor opens.</summary>
        public static void Ensure(RectTransform root)
        {
            if (root == null || (_host != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _generation = UiTheme.Generation;
            Tiles.Clear();
            Shown.Clear();
            Chips.Clear();
            _catalogGeneration = -1;
            Build(root);
        }

        /// <summary>Opens the popup. False when it cannot be: no window, or a dialog is up.</summary>
        public static bool Open()
        {
            if (!ModUi.Open || _host == null || Dialogs.IsOpen)
            {
                return IsOpen;
            }

            if (IsOpen)
            {
                return true;
            }

            FocusNav.Leave();
            Header.CloseMenu();
            Fit();
            _host.SetActive(true);
            _host.transform.SetAsLastSibling();
            PieceCatalog.Ensure();
            var visible = PieceCatalog.Visible;
            if (_catalogGeneration != PieceCatalog.Generation || _catalogCount != visible.Count || Tiles.Count == 0)
            {
                BuildTiles(visible);
            }

            Filter();
            if (!EditorInput.PadInUse)
            {
                FocusSearch();
            }

            return true;
        }

        /// <summary>Closes the popup. True when it was open.</summary>
        public static bool Close()
        {
            if (!IsOpen)
            {
                return false;
            }

            if (ModUi.Typing)
            {
                FocusNav.StopTyping();
            }

            _host.SetActive(false);
            return true;
        }

        public static void Toggle()
        {
            if (!Close())
            {
                Open();
            }
        }

        /// <summary>
        /// Once a frame while open. The search box owns the keyboard, so the keys the popup needs while
        /// typing are read here: Tab (or the Quick add key) and Esc close it, up and down move the light.
        /// </summary>
        public static void Tick()
        {
            if (!IsOpen || !ModUi.Typing)
            {
                return;
            }

            if (Keymap.TypedPressed(Act.QuickAdd) || ZInput.GetKeyDown(KeyCode.Escape, false))
            {
                Close();
                return;
            }

            if (ZInput.GetKeyDown(KeyCode.DownArrow, false))
            {
                Move(0, 1);
            }
            else if (ZInput.GetKeyDown(KeyCode.UpArrow, false))
            {
                Move(0, -1);
            }
        }

        /// <summary>Puts the keyboard in the search box, so typing finds a piece at once.</summary>
        public static void FocusSearch()
        {
            if (_search == null || EventSystem.current == null || !IsOpen)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(_search.gameObject);
            _search.ActivateInputField();
        }

        public static void SetSearch(string text)
        {
            if (_search != null && _search.text != (text ?? ""))
            {
                _search.text = text ?? "";   // fires onValueChanged, which filters
                return;
            }

            Filter();
        }

        public static void SetTag(string tag)
        {
            _tag = tag;
            Filter();
        }

        /// <summary>The next or the previous tag chip, wrapping. L1 and R1 on the pad.</summary>
        public static void NextTag(int direction)
        {
            if (Chips.Count == 0)
            {
                return;
            }

            var at = Chips.FindIndex(c => c.Key == _tag);
            var next = ((at < 0 ? 0 : at) + direction + Chips.Count) % Chips.Count;
            SetTag(Chips[next].Key);
        }

        /// <summary>Moves the light by columns and rows, and scrolls it into view.</summary>
        public static void Move(int dx, int dy)
        {
            if (Shown.Count == 0)
            {
                return;
            }

            var to = Mathf.Clamp((_lit < 0 ? 0 : _lit) + dx + (dy * Columns), 0, Shown.Count - 1);
            Light(to);
        }

        /// <summary>Places the lit piece: it goes in hand and the popup closes.</summary>
        public static void Pick()
        {
            var entry = Lit;
            if (entry != null)
            {
                Choose(entry);
            }
        }

        /// <summary>Stars a piece or takes the star off. Right click on a tile; the test calls it without a mouse.</summary>
        public static void ToggleStar(PieceEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var on = PieceMemory.ToggleFavourite(entry.PrefabName);
            foreach (var tile in Tiles)
            {
                tile.Refresh(false);
            }

            RefreshCounts();

            if (_tag == FavouriteKey)
            {
                Filter();
            }

            Toasts.Info(on ? $"Starred {entry.DisplayName}." : $"Unstarred {entry.DisplayName}.");
        }

        private static void Choose(PieceEntry entry)
        {
            Close();
            PieceChosen?.Invoke(entry);
        }

        // ---------- filtering ----------

        private static void Filter()
        {
            if (_host == null)
            {
                return;
            }

            RefreshCounts();

            var words = (Search ?? "").ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Shown.Clear();
            foreach (var tile in Tiles)
            {
                var on = Matches(tile.Entry, words);
                tile.Root.gameObject.SetActive(on);
                if (on)
                {
                    Shown.Add(tile);
                }
            }

            // Names that start with what was typed first, then the rest; Recent newest first.
            if (_tag == RecentKey)
            {
                Reorder(t => PieceMemory.RecentIndex(t.Entry.PrefabName));
            }
            else if (words.Length > 0)
            {
                var first = words[0];
                Reorder(t => Rank(t, first));
            }

            for (var i = 0; i < Shown.Count; i++)
            {
                Shown[i].Root.SetSiblingIndex(i);
            }

            foreach (var chip in Chips)
            {
                chip.Set(chip.Key == _tag);
            }

            _count.text = $"{Shown.Count} of {Tiles.Count}";
            _empty.gameObject.SetActive(Shown.Count == 0);
            _empty.text = _tag == FavouriteKey && words.Length == 0
                ? "No starred pieces yet. Right click a piece to star it."
                : _tag == RecentKey && words.Length == 0 ? "Nothing placed yet." : "No piece matches.";
            _scroll.content.anchoredPosition = Vector2.zero;
            Light(Shown.Count > 0 ? 0 : -1);
        }

        /// <summary>The exact prefab first, then names that start with the word, then the rest.</summary>
        private static int Rank(Tile tile, string first)
        {
            if (string.Equals(tile.Entry.PrefabName, first, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return tile.Entry.DisplayName.ToLowerInvariant().StartsWith(first, StringComparison.Ordinal) ? 1 : 2;
        }

        private static bool Matches(PieceEntry entry, string[] words)
        {
            if (_tag == RecentKey && PieceMemory.RecentIndex(entry.PrefabName) < 0)
            {
                return false;
            }

            if (_tag == FavouriteKey && !PieceMemory.IsFavourite(entry.PrefabName))
            {
                return false;
            }

            if (_tag != null && _tag != RecentKey && _tag != FavouriteKey
                && (entry.UsageTags == null || Array.IndexOf(entry.UsageTags, _tag) < 0))
            {
                return false;
            }

            foreach (var word in words)
            {
                if (entry.SearchText.IndexOf(word, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Light(int index, bool scroll = true)
        {
            if (_lit >= 0 && _lit < Shown.Count)
            {
                Shown[_lit].Refresh(false);
            }

            _lit = index;
            if (_lit < 0 || _lit >= Shown.Count)
            {
                _info.text = "";
                return;
            }

            var tile = Shown[_lit];
            tile.Refresh(true);
            _info.text = Info(tile.Entry);
            if (scroll)
            {
                ScrollTo(_lit);
            }
        }

        /// <summary>One line about a piece: its name, size, cost and station.</summary>
        private static string Info(PieceEntry entry)
        {
            var size = entry.Bounds.size;
            var cost = entry.Cost.Length == 0 ? "free" : string.Join(", ", entry.Cost.Select(c => $"{c.Amount} {c.Name}"));
            var station = entry.StationName != null ? "  ·  " + entry.StationName : "";
            return $"{entry.DisplayName}  ·  {size.x:0.#} × {size.z:0.#} × {size.y:0.#} m  ·  {cost}{station}";
        }

        private static void ScrollTo(int index)
        {
            var view = _scroll.viewport.rect.height;
            var rowHeight = _grid.cellSize.y + Spacing;
            var top = (index / Columns) * rowHeight;
            var at = _scroll.content.anchoredPosition;
            if (top < at.y)
            {
                at.y = top;
            }
            else if (top + rowHeight > at.y + view)
            {
                at.y = top + rowHeight - view;
            }

            _scroll.content.anchoredPosition = at;
        }

        // ---------- widgets ----------

        private static void Fit()
        {
            var root = EditorWindow.Root;
            if (_card == null || root == null)
            {
                return;
            }

            var width = Mathf.Clamp(root.rect.width - 80f, 560f, 900f);
            var height = Mathf.Clamp(root.rect.height - 180f, 320f, 580f);
            _card.sizeDelta = new Vector2(width, height);
            var inner = width - 28f - SideWidth - 10f;
            Columns = Mathf.Max(3, Mathf.FloorToInt((inner + Spacing) / (TileTarget + Spacing)));
            _grid.constraintCount = Columns;
            var cell = Mathf.Floor((inner - ((Columns - 1) * Spacing)) / Columns);
            _grid.cellSize = new Vector2(cell, cell + 16f);
            _scroll.GetComponent<WheelScroll>().Step = 2f * (_grid.cellSize.y + Spacing);
        }

        private static void Build(RectTransform root)
        {
            var dim = UiBuild.Panel("QuickAdd", root, null, new Color(0f, 0f, 0f, 0.4f));
            dim.type = Image.Type.Simple;
            UiBuild.Stretch(dim.rectTransform);
            _host = dim.gameObject;
            dim.gameObject.AddComponent<ClickEvents>().Clicked = _ => Close();

            var card = UiBuild.Card("Card", dim.transform);
            _card = card.rectTransform;
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 1f);
            _card.pivot = new Vector2(0.5f, 1f);
            _card.anchoredPosition = new Vector2(0f, -(EditorWindow.TopBand + 36f));
            _card.sizeDelta = new Vector2(900f, 560f);
            card.gameObject.AddComponent<ClickEvents>();   // a click on the card itself stays inside

            _search = UiBuild.InputField("Search", _card, "Search pieces: wall, roof, door…", SearchHeight);
            _search.textComponent.fontSize = 16f;
            ((TMP_Text)_search.placeholder).fontSize = 16f;
            _search.pointSize = 16f;
            var search = (RectTransform)_search.transform;
            Top(search, 0f, SearchHeight);
            _search.GetComponent<FieldLook>().Plain = true;
            ((Image)_search.targetGraphic).color = new Color(0f, 0f, 0f, 0f);
            var outline = _search.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            _search.textViewport.offsetMin = new Vector2(18f, _search.textViewport.offsetMin.y);
            _search.onValueChanged.AddListener(_ => Filter());
            _search.onSubmit.AddListener(_ => Pick());
            var close = Kit.Text(_card, "Tab closes", Kit.CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            close.rectTransform.anchorMin = new Vector2(1f, 1f);
            close.rectTransform.anchorMax = new Vector2(1f, 1f);
            close.rectTransform.pivot = new Vector2(1f, 1f);
            close.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            close.rectTransform.sizeDelta = new Vector2(120f, SearchHeight);
            var line = Kit.Divider(_card);
            Top(line.rectTransform, SearchHeight, 1f);

            // The categories: a list down the left side, each with how many pieces it holds.
            var categories = UiBuild.Scroll("Categories", _card, 2f);
            var side = (RectTransform)categories.transform;
            side.anchorMin = new Vector2(0f, 0f);
            side.anchorMax = new Vector2(0f, 1f);
            side.pivot = new Vector2(0f, 0.5f);
            side.offsetMin = new Vector2(14f, FootHeight + 6f);
            side.offsetMax = new Vector2(14f + SideWidth, -(SearchHeight + 10f));
            categories.GetComponent<WheelScroll>().Step = 3f * ChipHeight;
            _chipRow = categories.content;
            var divider = Kit.Divider(_card, true);
            var dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(0f, 1f);
            dividerRect.pivot = new Vector2(0f, 0.5f);
            dividerRect.offsetMin = new Vector2(14f + SideWidth + 4f, FootHeight + 6f);
            dividerRect.offsetMax = new Vector2(14f + SideWidth + 5f, -(SearchHeight + 10f));
            UnityEngine.Object.Destroy(divider.GetComponent<LayoutElement>());

            _scroll = UiBuild.Scroll("Grid", _card, 0f);
            var grid = (RectTransform)_scroll.transform;
            grid.anchorMin = Vector2.zero;
            grid.anchorMax = Vector2.one;
            grid.offsetMin = new Vector2(14f + SideWidth + 12f, FootHeight + 6f);
            grid.offsetMax = new Vector2(-14f, -(SearchHeight + 10f));
            var content = _scroll.content;
            UnityEngine.Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            _grid = content.gameObject.AddComponent<GridLayoutGroup>();
            _grid.spacing = new Vector2(Spacing, Spacing);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = Columns;
            _grid.cellSize = new Vector2(110f, 128f);

            _empty = Kit.Note(_scroll.viewport, "", Kit.BodySize, UiTheme.TextDim);
            _empty.alignment = TextAlignmentOptions.Top;
            UiBuild.Stretch(_empty.rectTransform, 0f, 0f, 0f, 40f);

            var foot = Kit.Divider(_card);
            foot.rectTransform.anchorMin = new Vector2(0f, 0f);
            foot.rectTransform.anchorMax = new Vector2(1f, 0f);
            foot.rectTransform.pivot = new Vector2(0.5f, 0f);
            foot.rectTransform.anchoredPosition = new Vector2(0f, FootHeight);
            foot.rectTransform.sizeDelta = new Vector2(0f, 1f);
            _info = Kit.Text(_card, "", Kit.CaptionSize, UiTheme.TextDim);
            Bottom(_info.rectTransform, 14f, 160f);
            _count = Kit.Text(_card, "", Kit.CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            Bottom(_count.rectTransform, 14f, 14f);
            _count.rectTransform.anchorMin = new Vector2(1f, 0f);
            _count.rectTransform.sizeDelta = new Vector2(140f, FootHeight);
            _count.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
            _count.rectTransform.pivot = new Vector2(1f, 0f);

            _host.SetActive(false);
        }

        private static void Top(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static void Bottom(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, FootHeight);
        }

        /// <summary>One tile per piece and the chips, built while the popup is open (see the class note).</summary>
        private static void BuildTiles(IReadOnlyList<PieceEntry> entries)
        {
            foreach (var tile in Tiles)
            {
                UnityEngine.Object.Destroy(tile.Root.gameObject);
            }

            Tiles.Clear();
            Shown.Clear();
            foreach (var entry in entries)
            {
                Tiles.Add(NewTile(entry));
            }

            foreach (var chip in Chips)
            {
                UnityEngine.Object.Destroy(chip.Back.gameObject);
            }

            Chips.Clear();
            Chips.Add(NewChip(null, "All"));
            Chips.Add(NewChip(RecentKey, "Recent"));
            Chips.Add(NewChip(FavouriteKey, "Starred"));
            foreach (var tag in PieceCatalog.Tags)
            {
                Chips.Add(NewChip(tag, tag));
            }

            if (_tag != null && !Chips.Exists(c => c.Key == _tag))
            {
                _tag = null;
            }

            RefreshCounts();
            _catalogGeneration = PieceCatalog.Generation;
            _catalogCount = entries.Count;
        }

        private static Tile NewTile(PieceEntry entry)
        {
            var back = UiBuild.Panel("Tile", _scroll.content, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            var tile = new Tile { Entry = entry, Root = back.rectTransform, Back = back };

            var icon = UiBuild.Panel("Icon", back.transform, entry.Icon);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = entry.Icon != null;
            icon.rectTransform.anchorMin = new Vector2(0f, 0f);
            icon.rectTransform.anchorMax = new Vector2(1f, 1f);
            icon.rectTransform.offsetMin = new Vector2(8f, 32f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var name = Kit.Note(back.transform, entry.DisplayName, 10f, UiTheme.Text);
            name.alignment = TextAlignmentOptions.Bottom;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableWordWrapping = true;
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.offsetMin = new Vector2(4f, 3f);
            name.rectTransform.offsetMax = new Vector2(-4f, 30f);
            tile.Name = name;

            var star = UiBuild.Panel("Star", back.transform, null, UiTheme.Accent);
            star.type = Image.Type.Simple;
            star.raycastTarget = false;
            star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0f, 1f);
            star.rectTransform.pivot = new Vector2(0f, 1f);
            star.rectTransform.anchoredPosition = new Vector2(6f, -6f);
            star.rectTransform.sizeDelta = new Vector2(7f, 7f);
            tile.Star = star;

            back.gameObject.AddComponent<ClickEvents>().Clicked = data =>
            {
                if (data.button == PointerEventData.InputButton.Right)
                {
                    ToggleStar(entry);
                }
                else if (data.button == PointerEventData.InputButton.Left)
                {
                    Choose(entry);
                }
            };
            back.gameObject.AddComponent<HoverEvents>().Changed = on =>
            {
                // Only a mouse that moved lights a tile: the grid reorders under a resting mouse while typing.
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (on && mouse != null && mouse.delta.ReadValue() != Vector2.zero)
                {
                    var index = Shown.IndexOf(tile);
                    if (index >= 0)
                    {
                        Light(index, false);
                    }
                }
            };
            tile.Refresh(false);
            return tile;
        }

        /// <summary>Sorts the shown tiles by a number, and keeps tiles with the same number in the order they had (a plain sort scrambles ties).</summary>
        private static void Reorder(System.Func<Tile, int> rank)
        {
            var ordered = Shown.Select((tile, index) => new { tile, index, rank = rank(tile) })
                .OrderBy(x => x.rank)
                .ThenBy(x => x.index)
                .Select(x => x.tile)
                .ToList();
            Shown.Clear();
            Shown.AddRange(ordered);
        }

        private static Chip NewChip(string key, string text)
        {
            var back = UiBuild.Panel("Category " + text, _chipRow, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            UiBuild.Rounded(back);
            Kit.Size(back, -1f, ChipHeight);
            var label = Kit.Text(back.transform, text, Kit.BodySize, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiBuild.Stretch(label.rectTransform, 12f, 0f, 44f, 0f);
            var count = Kit.Text(back.transform, "", Kit.CaptionSize, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            UiBuild.Stretch(count.rectTransform, 80f, 0f, 10f, 0f);
            back.gameObject.AddComponent<ClickEvents>().Clicked = _ => SetTag(key);
            var chip = new Chip { Key = key, Back = back, Label = label, Count = count };
            chip.Set(key == _tag);
            return chip;
        }

        /// <summary>How many pieces each category holds (All, Recent and Starred too), read again when pieces are starred or used.</summary>
        private static void RefreshCounts()
        {
            foreach (var chip in Chips)
            {
                var n = 0;
                foreach (var tile in Tiles)
                {
                    var entry = tile.Entry;
                    var holds = chip.Key == null
                        || (chip.Key == RecentKey ? PieceMemory.RecentIndex(entry.PrefabName) >= 0
                            : chip.Key == FavouriteKey ? PieceMemory.IsFavourite(entry.PrefabName)
                            : entry.UsageTags != null && Array.IndexOf(entry.UsageTags, chip.Key) >= 0);
                    n += holds ? 1 : 0;
                }

                chip.Count.text = n.ToString();
            }
        }

        private sealed class Tile
        {
            public PieceEntry Entry;
            public RectTransform Root;
            public Image Back;
            public TextMeshProUGUI Name;
            public Image Star;

            public void Refresh(bool lit)
            {
                Back.color = lit ? UiTheme.SurfacePressed : UiTheme.Surface;
                Name.color = lit ? UiTheme.Text : UiTheme.TextDim;
                Star.gameObject.SetActive(PieceMemory.IsFavourite(Entry.PrefabName));
                var outline = Back.GetComponent<Outline>();
                if (lit && outline == null)
                {
                    outline = Back.gameObject.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(2f, -2f);
                    outline.useGraphicAlpha = false;
                }

                if (outline != null)
                {
                    outline.effectColor = UiTheme.Accent;
                    outline.enabled = lit;
                }
            }
        }

        private sealed class Chip
        {
            public string Key;
            public Image Back;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Count;

            public void Set(bool on)
            {
                var fill = UiTheme.Accent;
                fill.a = on ? 0.28f : 0f;
                Back.color = fill;
                Label.color = on ? UiTheme.Text : UiTheme.TextDim;
                Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                Count.color = on ? UiTheme.Text : UiTheme.TextDim;
            }
        }
    }
}
