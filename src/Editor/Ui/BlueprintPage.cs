using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Blueprints;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The Inspector's Blueprint tab: the name and the one-line description the build card shows, the
    /// icon (the first piece, or one of the blueprint's kinds), a preview of the card, and the materials
    /// the whole blueprint needs against what the player has around them (the bag and the chests in
    /// range), with the crafting stations.
    ///
    /// The two text boxes write into the document on every keystroke; the document makes a run of
    /// keystrokes in one box a single undo step. The materials are worked out again once a second at
    /// most (<see cref="MaterialSources.Around"/> walks every loaded piece), and at once on a change.
    /// </summary>
    internal static class BlueprintPage
    {
        private const float ChoiceSize = 34f;

        /// <summary>While the window is open, the list is worked out again this often (seconds), and at once when the document changes.</summary>
        public const float MaterialsPeriod = 1f;

        private static RectTransform _root;
        private static TMP_InputField _name;
        private static TMP_InputField _description;
        private static RectTransform _choices;
        private static TextMeshProUGUI _iconWarning;
        private static TextMeshProUGUI _cardName;
        private static Image _cardIcon;
        private static TextMeshProUGUI _cardText;
        private static RectTransform _materials;
        private static LayoutElement _materialsSize;
        private static MaterialList _list;

        private static MaterialSources _sources;
        private static float _sourcesAt = float.MinValue;
        private static float _nextMaterials;

        private static readonly List<Choice> Choices = new List<Choice>();
        private static BlueprintDocument _document;
        private static int _revision = -1;
        private static int _catalogGeneration = -1;
        private static string _kindsKey = "";

        public static TMP_InputField NameField => _name;

        public static TMP_InputField DescriptionField => _description;

        public static int ChoiceCount => Choices.Count;

        public static string ChosenIcon => _document != null ? _document.IconPrefab : null;

        public static string CardName => _cardName != null ? _cardName.text : "";

        public static string CardText => _cardText != null ? _cardText.text : "";

        public static string IconWarningText => _iconWarning != null && _iconWarning.gameObject.activeSelf ? _iconWarning.text : "";

        public static MaterialList List => _list;

        public static Tally LastTally { get; private set; }

        public static int MaterialRefreshes { get; private set; }

        public static int SourceReads { get; private set; }

        /// <summary>Picks one of the icon choices, the way a click on it does.</summary>
        public static void Choose(int index)
        {
            if (index >= 0 && index < Choices.Count)
            {
                ChooseIcon(Choices[index].Prefab);
            }
        }

        public static void Show(BlueprintDocument document)
        {
            _document = document;
            _revision = -1;
            _kindsKey = "";
            Refresh();
        }

        public static void Close()
        {
            _document = null;
            _revision = -1;
            _sources = null;   // let go of the chests; the next open reads them again
        }

        public static void Tick()
        {
            if (_root == null || _document == null)
            {
                return;
            }

            if (_document.Revision != _revision || PieceCatalog.Generation != _catalogGeneration)
            {
                Refresh();
            }
            else if (Time.unscaledTime >= _nextMaterials)
            {
                FillMaterials();
            }
            else if (_list != null && LastTally != null && !Mathf.Approximately(ListWidth(), _list.Width))
            {
                ShowList();
            }
        }

        public static void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            _revision = _document != null ? _document.Revision : -1;
            _catalogGeneration = PieceCatalog.Generation;

            // Never overwrite the box being typed in: the caret would jump to the end.
            if (!_name.isFocused)
            {
                _name.SetTextWithoutNotify(_document != null ? _document.Name : "");
            }

            if (!_description.isFocused)
            {
                _description.SetTextWithoutNotify(_document != null ? _document.Description : "");
            }

            FillChoices();
            FillCard();
            FillMaterials();
        }

        private static void ChooseIcon(string prefabName)
        {
            if (_document == null)
            {
                return;
            }

            _document.SetIcon(prefabName);
            Refresh();
        }

        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var flat = new StringBuilder(text.Length);
            var wasBreak = false;
            foreach (var c in text)
            {
                if (c == '\r' || c == '\n')
                {
                    if (!wasBreak)
                    {
                        flat.Append(' ');
                    }

                    wasBreak = true;
                    continue;
                }

                flat.Append(c);
                wasBreak = false;
            }

            return flat.ToString();
        }

        private static void FillChoices()
        {
            var kinds = new List<string>();
            var seen = new HashSet<string>();
            if (_document != null)
            {
                foreach (var piece in _document.Pieces)
                {
                    if (seen.Add(piece.PrefabName))
                    {
                        kinds.Add(piece.PrefabName);
                    }
                }
            }

            var key = string.Join("|", kinds.ToArray());
            if (key != _kindsKey)
            {
                _kindsKey = key;
                foreach (var choice in Choices)
                {
                    Object.Destroy(choice.Back.gameObject);
                }

                Choices.Clear();
                Choices.Add(NewChoice(null, "First", null));
                foreach (var prefab in kinds)
                {
                    var entry = PieceCatalog.Find(prefab);
                    Choices.Add(NewChoice(prefab, entry != null ? null : "?", entry != null ? entry.Icon : null));
                }
            }

            var chosen = _document != null ? _document.IconPrefab : null;
            foreach (var choice in Choices)
            {
                var on = choice.Prefab == null ? string.IsNullOrEmpty(chosen) : choice.Prefab == chosen;
                choice.Back.color = on ? UiTheme.Accent : UiTheme.Surface;
                if (choice.Label != null)
                {
                    choice.Label.color = on ? UiTheme.TextOnAccent : UiTheme.Text;
                }
            }

            var missing = !string.IsNullOrEmpty(chosen) && !seen.Contains(chosen);
            _iconWarning.gameObject.SetActive(missing);
            if (missing)
            {
                _iconWarning.text = "#Icon:" + chosen + " is not in the blueprint.";
            }
        }

        private static void FillCard()
        {
            var card = BlueprintCard.Build(_document);
            _cardName.text = string.IsNullOrEmpty(card.Name) ? "(no name)" : card.Name;
            _cardIcon.sprite = card.Icon;
            _cardIcon.enabled = card.Icon != null;
            _cardText.text = card.Description;
        }

        private static void FillMaterials()
        {
            MaterialRefreshes++;
            _nextMaterials = Time.unscaledTime + MaterialsPeriod;
            var player = Player.m_localPlayer;
            var costsOff = player != null && player.PlacementCostDisabled;
            if (player != null && !costsOff && _document != null && _document.Pieces.Count > 0
                && (_sources == null || Time.unscaledTime - _sourcesAt >= MaterialsPeriod))
            {
                _sources = MaterialSources.Around(player);
                _sourcesAt = Time.unscaledTime;
                SourceReads++;
            }

            LastTally = BlueprintCard.Materials(
                _document,
                costsOff ? null : _sources,
                costsOff,
                player != null ? player.transform.position : (Vector3?)null);
            ShowList();
        }

        private static void ShowList()
        {
            if (_list == null || LastTally == null)
            {
                return;
            }

            _list.MaxColumns = 1;
            _list.Width = ListWidth();
            _list.Show(LastTally);
            if (!Mathf.Approximately(_materialsSize.preferredHeight, _list.Height))
            {
                _materialsSize.minHeight = _materialsSize.preferredHeight = _list.Height;
            }
        }

        private static float ListWidth()
        {
            var width = _materials != null ? _materials.rect.width : 0f;
            return width > 1f ? width : EditorWindow.RightWidth - 28f;
        }

        // ---------- widgets ----------

        public static void Build(RectTransform page)
        {
            var scroll = Kit.Scroll(page, 14f, 14);
            UiBuild.Stretch((RectTransform)scroll.transform);
            _root = scroll.content;
            Choices.Clear();
            _kindsKey = "";

            var about = Kit.Column(_root, 6f, 0, "About");
            Caption(about, "Name");
            _name = Kit.Field(about, "", "The name on the build card");
            _name.onValueChanged.AddListener(text => _document?.SetName(text));
            Caption(about, "Description");
            _description = Kit.Field(about, "", "One line, under the name");
            _description.onValueChanged.AddListener(text =>
            {
                // A header line can never carry a line break, so a pasted one becomes a space.
                var flat = OneLine(text);
                _document?.SetDescription(flat);
                if (flat != text)
                {
                    _description.SetTextWithoutNotify(flat);
                }
            });

            var icon = Kit.Column(_root, 6f, 0, "Icon");
            Caption(icon, "Icon");
            _choices = UiBuild.Rect("Choices", icon);
            var grid = _choices.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ChoiceSize, ChoiceSize);
            grid.spacing = new Vector2(4f, 4f);
            Kit.Fit(_choices, false, true);
            _iconWarning = Kit.Note(icon, "", Kit.CaptionSize, UiTheme.Warn);
            _iconWarning.gameObject.SetActive(false);

            // The card as the game shows it, then everything it costs.
            var card = UiBuild.Panel("Card", _root, null, UiTheme.Inset);
            card.type = Image.Type.Simple;
            UiBuild.Border(card);
            var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6f;
            column.padding = new RectOffset(10, 10, 10, 10);
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            Caption(card.transform, "On the build card");
            var top = Kit.Row(card.transform, 10f, 44f);
            _cardIcon = UiBuild.Panel("Icon", top, null);
            _cardIcon.type = Image.Type.Simple;
            _cardIcon.preserveAspect = true;
            Kit.Size(_cardIcon, 40f, 40f);
            var names = Kit.Column(top, 2f);
            Kit.Size(names, -1f, -1f, 1f);
            _cardName = Kit.Text(names, "", Kit.BodySize, UiTheme.Text);
            _cardName.fontStyle = FontStyles.Bold;
            _cardText = Kit.Note(names, "", Kit.CaptionSize, UiTheme.TextDim);

            Caption(card.transform, "Materials, against what you have here");
            _materials = UiBuild.Rect("Materials", card.transform);
            _materialsSize = _materials.gameObject.AddComponent<LayoutElement>();
            _list = MaterialList.Create(_materials, EditorWindow.RightWidth - 48f, 1);
            _list.FooterShown = false;
        }

        private static void Caption(Transform parent, string text)
        {
            var label = Kit.Text(parent, text, Kit.CaptionSize, UiTheme.TextDim);
            Kit.Size(label, -1f, 16f);
        }

        private static Choice NewChoice(string prefab, string text, Sprite icon)
        {
            var back = UiBuild.Panel("Choice", _choices, null, UiTheme.Surface);
            back.type = Image.Type.Simple;
            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ChooseIcon(prefab));

            var image = UiBuild.Panel("Icon", back.transform, icon);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = icon != null;
            UiBuild.Stretch(image.rectTransform, 3f, 3f, 3f, 3f);

            TextMeshProUGUI label = null;
            if (text != null)
            {
                label = Kit.Text(back.transform, text, 11f, UiTheme.Text, TextAlignmentOptions.Center);
                UiBuild.Stretch(label.rectTransform, 1f, 1f, 1f, 1f);
            }

            return new Choice { Prefab = prefab, Back = back, Label = label };
        }

        private sealed class Choice
        {
            public string Prefab;
            public Image Back;
            public TextMeshProUGUI Label;
        }
    }
}
