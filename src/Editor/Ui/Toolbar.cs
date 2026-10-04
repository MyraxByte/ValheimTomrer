using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValheimTomrer.Editor.View;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The floating bar at the bottom of the view, like a design tool's: how pieces land (the grid, the
    /// turn step, the snap points), how the view draws them (snap dots, boxes), and hiding the panels.
    /// Each button shows its setting and changes it with a click; every one also has a key.
    /// The pad walk has it as its own region (<see cref="FocusRegion.Bottom"/>).
    /// </summary>
    internal static class Toolbar
    {
        private static RectTransform _host;
        private static RectTransform _root;
        private static int _generation = -1;
        private static float _gridShown = -1f;
        private static float _turnShown = -1f;
        private static Button _grid;
        private static Button _turn;
        private static Button _points;
        private static Button _dots;
        private static Button _boxes;
        private static Button _sizes;
        private static Button _ruler;
        private static Button _light;
        private static Button _support;
        private static Button _lines;

        /// <summary>The bar itself, for the pad walk.</summary>
        public static RectTransform Root => _root;

        public static string GridText => _grid != null ? Kit.LabelOf(_grid).text : "";

        public static void Ensure(RectTransform host)
        {
            if (host == null || (_host == host && _root != null && _generation == UiTheme.Generation))
            {
                return;
            }

            _host = host;
            _generation = UiTheme.Generation;
            _gridShown = _turnShown = -1f;
            Build(host);
        }

        public static void Tick()
        {
            if (_root == null)
            {
                return;
            }

            // The words are rebuilt only when the number changed.
            var grid = EditorState.GridStep;
            if (!Mathf.Approximately(grid, _gridShown))
            {
                _gridShown = grid;
                Set(_grid, grid > 0f ? $"Grid {grid:0.##} m" : "Grid off", grid > 0f);
            }

            var turn = EditorState.AngleStep;
            if (!Mathf.Approximately(turn, _turnShown))
            {
                _turnShown = turn;
                Set(_turn, $"Turn {turn:0.##}°", false);
            }

            var points = EditorConfig.SnapPoints == null || EditorConfig.SnapPoints.Value;
            Set(_points, points ? "Snap points" : "Snap points off", points);
            Set(_dots, "Dots", EditorState.SnapDotsOn);
            Set(_boxes, "Boxes", EditorState.PieceBoxesOn);
            Set(_sizes, "Sizes", EditorState.DimensionsOn);
            Set(_ruler, "Ruler", ViewportHost.RulerOn);
            Kit.SetLabel(_light, "Light: " + SceneLook.Name(SceneLook.Now));
            Set(_support, "Support", EditorState.SupportColoursOn);
            Set(_lines, "Lines", EditorState.GridShown);

            // Wider than the room between the cards: smaller, never under a card.
            var room = EditorWindow.Root.rect.width - EditorWindow.FreeLeft - EditorWindow.FreeRight;
            var wide = LayoutUtility.GetPreferredWidth(_root);
            var scale = wide > room && wide > 1f ? Mathf.Clamp(room / wide, 0.55f, 1f) : 1f;
            if (!Mathf.Approximately(_root.localScale.x, scale))
            {
                _root.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static void Set(Button button, string text, bool on)
        {
            Kit.SetLabel(button, text);
            Kit.LabelOf(button).color = on ? UiTheme.Accent : UiTheme.Text;
        }

        private static void Build(RectTransform host)
        {
            var card = UiBuild.Card("Toolbar", host);
            _root = card.rectTransform;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = Vector2.zero;
            var row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 2f;
            row.padding = new RectOffset(4, 4, 4, 4);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Kit.Fit(_root, true, true);

            var walk = new List<Selectable>();
            _grid = Add(walk, Kit.Ghost(card.transform, "Grid 0.25 m", EditorCommands.CycleGrid, 30f));
            _turn = Add(walk, Kit.Ghost(card.transform, "Turn 22.5°", EditorCommands.CycleAngle, 30f));
            _points = Add(walk, Kit.Ghost(card.transform, "Snap points off", EditorCommands.ToggleSnapPoints, 30f));
            Kit.Divider(card.transform, true);
            _dots = Add(walk, Kit.Ghost(card.transform, "Dots", EditorCommands.ToggleSnapDots, 30f));
            _boxes = Add(walk, Kit.Ghost(card.transform, "Boxes", EditorCommands.ToggleBoxes, 30f));
            _sizes = Add(walk, Kit.Ghost(card.transform, "Sizes", EditorCommands.ToggleDimensions, 30f));
            _ruler = Add(walk, Kit.Ghost(card.transform, "Ruler", ViewportHost.ToggleRuler, 30f));
            _light = Add(walk, Kit.Ghost(card.transform, "Light: Day", EditorCommands.CycleTimeOfDay, 30f));
            _support = Add(walk, Kit.Ghost(card.transform, "Support", EditorCommands.ToggleSupportColours, 30f));
            _lines = Add(walk, Kit.Ghost(card.transform, "Lines", EditorCommands.ToggleGridLines, 30f));
            Kit.Divider(card.transform, true);
            Add(walk, Kit.Ghost(card.transform, "Hide panels", () => EditorWindow.SetUiHidden(true), 30f));
            UiBuild.LinkRow(walk, true);
            Tick();
        }

        private static Button Add(List<Selectable> walk, Button button)
        {
            walk.Add(button);
            return button;
        }
    }
}
