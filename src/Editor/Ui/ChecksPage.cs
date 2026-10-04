using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The Inspector's Checks tab: everything that is wrong with the blueprint (<see cref="Checks"/>),
    /// worst first, each with a coloured mark for its level. A click on a row selects the pieces it is
    /// about. The tab's title counts them. Rows are plain images, so the pad walk leaves them to the
    /// mouse.
    /// </summary>
    internal static class ChecksPage
    {
        private static RectTransform _root;
        private static TextMeshProUGUI _summary;
        private static TextMeshProUGUI _clean;
        private static readonly List<Row> Pool = new List<Row>();
        private static readonly List<Check> Found = new List<Check>();
        private static BlueprintDocument _document;
        private static int _revision = -1;
        private static int _catalogGeneration = -1;

        public static IReadOnlyList<Check> Rows => Found;

        public static int RowCount => Found.Count;

        public static string SummaryText => _summary != null ? _summary.text : "";

        public static string RowText(int index)
        {
            return index >= 0 && index < Pool.Count && Pool[index].Back.gameObject.activeSelf ? Pool[index].Label.text : "";
        }

        /// <summary>A click on a row: its pieces are selected.</summary>
        public static void Click(int index)
        {
            if (index < 0 || index >= Found.Count)
            {
                return;
            }

            var pieces = Found[index].Pieces;
            if (pieces != null && pieces.Length > 0)
            {
                EditorState.Select(pieces);
            }
        }

        public static void Show(BlueprintDocument document)
        {
            _document = document;
            _revision = -1;
            Refresh();
        }

        public static void Close()
        {
            _document = null;
            Found.Clear();
            _revision = -1;
        }

        public static void Tick()
        {
            if (_root == null || _document == null)
            {
                return;
            }

            if (_document.PiecesRevision != _revision || PieceCatalog.Generation != _catalogGeneration)
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

            _revision = _document != null ? _document.PiecesRevision : -1;
            _catalogGeneration = PieceCatalog.Generation;
            Found.Clear();
            Found.AddRange(Checks.Run(_document));

            _summary.text = Checks.Summary(Found);
            _clean.gameObject.SetActive(Found.Count == 0);
            while (Pool.Count < Found.Count)
            {
                Pool.Add(NewRow(Pool.Count));
            }

            for (var i = 0; i < Pool.Count; i++)
            {
                var on = i < Found.Count;
                Pool[i].Back.gameObject.SetActive(on);
                if (on)
                {
                    Pool[i].Show(Found[i]);
                }
            }
        }

        public static void Build(RectTransform page)
        {
            Pool.Clear();
            var scroll = Kit.Scroll(page, 6f, 14);
            UiBuild.Stretch((RectTransform)scroll.transform);
            _root = scroll.content;
            _summary = Kit.Text(_root, "", Kit.CaptionSize, UiTheme.TextDim);
            Kit.Size(_summary, -1f, 16f);
            _clean = Kit.Text(_root, "No problems found.", Kit.BodySize, UiTheme.Good);
            Kit.Size(_clean, -1f, 20f);
        }

        private static Row NewRow(int index)
        {
            var back = UiBuild.Panel("Row", _root, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            var layout = back.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var mark = UiBuild.Panel("Mark", back.transform, null, UiTheme.TextDim);
            mark.type = Image.Type.Simple;
            mark.raycastTarget = false;
            Kit.Size(mark, 3f, 16f);

            var label = Kit.Note(back.transform, "", Kit.BodySize, UiTheme.Text);
            Kit.Size(label, -1f, -1f, 1f);
            back.gameObject.AddComponent<ClickEvents>().Clicked = _ => Click(index);
            return new Row { Back = back, Mark = mark, Label = label };
        }

        private sealed class Row
        {
            public Image Back;
            public Image Mark;
            public TextMeshProUGUI Label;

            public void Show(Check check)
            {
                Label.text = check.LevelWord + ": " + check.Message;
                Mark.color = check.Level == CheckLevel.Error ? UiTheme.Warn
                    : check.Level == CheckLevel.Warning ? UiTheme.Accent
                    : UiTheme.TextDim;
                var clickable = check.Pieces != null && check.Pieces.Length > 0;
                Back.color = clickable ? UiTheme.Surface : UiTheme.SlotDim;
            }
        }
    }
}
