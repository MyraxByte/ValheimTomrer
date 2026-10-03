using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Editor.Doc;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The right panel: three tabs, Design (the selection: where it is, how it turns, align and arrange),
    /// Blueprint (name, description, icon, materials) and Checks (what is wrong, a click selects it). One
    /// tab shows at a time; the others are slid out of sight, never switched off, because their text is
    /// measured while they tick.
    /// </summary>
    internal static class Inspector
    {
        public const int DesignTab = 0;
        public const int BlueprintTab = 1;
        public const int ChecksTab = 2;
        private const float TabsHeight = 36f;
        private const float Away = 6000f;

        private static readonly string[] Names = { "Design", "Blueprint", "Checks" };

        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;
        private static TabStrip _tabs;
        private static readonly RectTransform[] Pages = new RectTransform[3];

        /// <summary>The tab open now. Kept across a rebuild and a close.</summary>
        public static int Tab { get; private set; }

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

        public static void Show(BlueprintDocument document)
        {
            DesignPage.Show();
            BlueprintPage.Show(document);
            ChecksPage.Show(document);
        }

        public static void Tick()
        {
            if (_root == null)
            {
                return;
            }

            DesignPage.Tick();
            BlueprintPage.Tick();
            ChecksPage.Tick();
            var problems = ChecksPage.RowCount;
            Kit.SetLabel(_tabs.Buttons[ChecksTab], problems > 0 ? $"Checks {problems}" : "Checks");
        }

        public static void Close()
        {
            DesignPage.Close();
            BlueprintPage.Close();
            ChecksPage.Close();
        }

        public static void SetTab(int tab)
        {
            Tab = Mathf.Clamp(tab, 0, Pages.Length - 1);
            if (_tabs == null)
            {
                return;
            }

            _tabs.Set(Tab);
            for (var i = 0; i < Pages.Length; i++)
            {
                Pages[i].anchoredPosition = i == Tab ? Vector2.zero : new Vector2(Away, 0f);
            }
        }

        private static void Build(RectTransform host)
        {
            _root = UiBuild.Rect("Inspector", host);
            UiBuild.Stretch(_root);

            _tabs = Kit.Tabs(_root, Names, SetTab);
            var tabs = _tabs.Root;
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(0f, -TabsHeight);
            tabs.offsetMax = Vector2.zero;
            var line = Kit.Divider(_root);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -TabsHeight);
            line.rectTransform.sizeDelta = new Vector2(0f, 1f);

            for (var i = 0; i < Pages.Length; i++)
            {
                var page = UiBuild.Rect(Names[i] + "Page", _root);
                UiBuild.Stretch(page, 0f, 0f, 0f, TabsHeight + 1f);
                Pages[i] = page;
            }

            DesignPage.Build(Pages[DesignTab]);
            BlueprintPage.Build(Pages[BlueprintTab]);
            ChecksPage.Build(Pages[ChecksTab]);
            SetTab(Tab);
        }
    }
}
