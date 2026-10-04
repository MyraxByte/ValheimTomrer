using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

namespace ValheimTomrer.Editor.Ui
{
    /// <summary>
    /// The editor's look: flat, modern panels in a Dark or a Light theme, a clean sans font, and a
    /// blue accent. It does not copy the game's wood on purpose: the editor is a tool, and it reads
    /// best like one. Every colour is plain code; the font is one the game already loaded. Nothing
    /// is shipped on disk and nothing is written.
    ///
    /// The game's own look is still here for the widgets that sit inside the game's HUD (the
    /// materials list on the hammer's build card, the capture's status line): code that builds or
    /// refreshes those wraps itself in <see cref="GameLook"/>, and every colour and the font answer
    /// with the game's while that is open.
    ///
    /// The HUD is rebuilt on every world load, so the cache is keyed on the live Hud object.
    /// SpriteAtlas.GetSprite hands out a copy, so the copies are destroyed on rebuild.
    /// </summary>
    internal static class UiTheme
    {
        private static int _gameLook;

        /// <summary>
        /// Inside this scope every colour and the font are the game's (white text, orange accent, the HUD
        /// font). For widgets that live in the game's own HUD. Scopes nest.
        /// </summary>
        public static System.IDisposable GameLook()
        {
            _gameLook++;
            return new Scope();
        }

        private sealed class Scope : System.IDisposable
        {
            private bool _done;

            public void Dispose()
            {
                if (!_done)
                {
                    _done = true;
                    _gameLook--;
                }
            }
        }

        public static bool InGame => _gameLook > 0;

        /// <summary>True in the Dark theme (the default). Read live: a switch shows at the next build of the window.</summary>
        public static bool Dark => EditorConfig.Theme == null || EditorConfig.Theme.Value == EditorTheme.Dark;

        public static Color Text => InGame ? Color.white : Dark ? Hex(0xE8EAED) : Hex(0x1C1F24);

        /// <summary>Captions, footers, hints and placeholders.</summary>
        public static Color TextDim => InGame ? Color.white : Dark ? Hex(0x9AA0A9) : Hex(0x667080);

        public static Color Accent => InGame ? (Color)new Color32(0xFF, 0xB4, 0x4C, 0xFF) : Dark ? Hex(0x4C8DFF) : Hex(0x0B6CF0);

        /// <summary>The label on an accent chip or button.</summary>
        public static Color TextOnAccent => Color.white;

        public static Color Warn => InGame ? (Color)new Color32(0xE8, 0x6A, 0x4A, 0xFF) : Dark ? Hex(0xFF6B5E) : Hex(0xD9372B);

        public static Color Good => InGame ? (Color)new Color32(0x8C, 0xD0, 0x7A, 0xFF) : Dark ? Hex(0x4CC77F) : Hex(0x1E9E55);

        public static Color Backdrop => InGame ? new Color(0f, 0f, 0f, 0.65f) : new Color(0f, 0f, 0f, Dark ? 0.55f : 0.35f);

        /// <summary>A floating card: the Layers and Inspector cards, the top bar, dialogs, popups, toasts.</summary>
        public static Color PanelFloat => Dark ? Hex(0x1C1D21) : Hex(0xFFFFFF);

        /// <summary>The soft shadow under an island, so it reads as floating over the view.</summary>
        public static Color IslandShadow => new Color(0f, 0f, 0f, Dark ? 0.45f : 0.22f);

        /// <summary>The same as <see cref="PanelFloat"/>. Kept for the code that still says interior.</summary>
        public static Color PanelInterior => PanelFloat;

        /// <summary>A row, a tile, a chip or a button on a card.</summary>
        public static Color Surface => InGame ? new Color(0.17f, 0.14f, 0.11f, 0.94f) : Dark ? Hex(0x2A2C31) : Hex(0xEEF0F3);

        /// <summary>The same under the mouse.</summary>
        public static Color SurfaceHover => Dark ? Hex(0x363940) : Hex(0xE1E4E9);

        /// <summary>A pressed button.</summary>
        public static Color SurfacePressed => Dark ? Hex(0x41454D) : Hex(0xD3D7DD);

        /// <summary>The inside of a text box.</summary>
        public static Color Field => Dark ? Hex(0x0E0F12) : Hex(0xF3F5F8);

        /// <summary>The outline of a text box at rest: strong enough that the box reads as one you can type in.</summary>
        public static Color FieldBorder => Dark ? Hex(0x4A4F59) : Hex(0xB3BBC7);

        /// <summary>The same under the mouse.</summary>
        public static Color FieldBorderHover => Dark ? Hex(0x7A8190) : Hex(0x7D8797);

        /// <summary>The thin line round a card, a text box or a key cap.</summary>
        public static Color Border => Dark ? Hex(0x34373D) : Hex(0xD6DAE0);

        /// <summary>A row or a tile that carries text.</summary>
        public static Color Slot => Surface;

        /// <summary>The same, weaker, for a row that cannot be clicked.</summary>
        public static Color SlotDim => Alpha(Surface, 0.6f);

        /// <summary>A sunken area inside a card, such as the build card copy.</summary>
        public static Color Inset => Dark ? Hex(0x16171A) : Hex(0xF6F7F9);

        /// <summary>The hint text drawn straight over the 3D picture: light on the dark scene, dark on the light one.</summary>
        public static Color TextOnPicture => Dark ? Hex(0xD9DCE1) : Hex(0x2A2F36);

        /// <summary>A key cap in the hint row over the picture.</summary>
        public static Color Cap => Dark ? Alpha(Hex(0x2A2C31), 0.92f) : Alpha(Hex(0xFFFFFF), 0.92f);

        public static Color Viewport => SceneBackground;

        // The 3D pane's own colours: a neutral canvas, like a design tool's.
        public static Color SceneBackground => Dark ? Hex(0x17191D) : Hex(0xE6E9ED);
        public static Color SceneAmbient => Dark ? new Color(0.30f, 0.32f, 0.36f, 1f) : new Color(0.48f, 0.50f, 0.54f, 1f);
        public static Color SceneGround => Dark ? Hex(0x23262B) : Hex(0xD2D6DC);
        public static Color SceneGrid => Dark ? new Color(0.62f, 0.68f, 0.78f, 0.16f) : new Color(0.22f, 0.27f, 0.34f, 0.20f);
        public static Color SceneRing => Dark ? Hex(0xC9CED6) : Hex(0x2A2F36);

        /// <summary>The selection's boxes and the box drag in the pane.</summary>
        public static Color Selection => Dark ? Hex(0x4C8DFF) : Hex(0x0B6CF0);

        private static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }

        private static Color Alpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }

        /// <summary>
        /// Makes every panel build again at its next Ensure. The sprites and fonts are kept: only
        /// the colours changed. See <see cref="EditorSession.Retheme"/>.
        /// </summary>
        public static void Rebuild()
        {
            Generation++;
        }

        private static TMP_FontAsset _font;
        private static TMP_FontAsset _gameFont;
        private static Material _fontMaterial;
        private static Material _fontOutlined;
        private static Material _gameMaterial;
        private static Material _gameOutlined;

        /// <summary>The editor's font: a plain sans the game ships, else the HUD's. The game's inside <see cref="GameLook"/>.</summary>
        public static TMP_FontAsset Font => InGame ? _gameFont : _font;

        public static Material FontMaterial => InGame ? _gameMaterial : _fontMaterial;

        /// <summary>
        /// The same, with a dark outline and a shadow. For the text drawn straight over the 3D
        /// picture, where the background is whatever the camera happens to be looking at.
        /// </summary>
        public static Material FontOutlined => InGame ? _gameOutlined : _fontOutlined;

        public static Sprite Panel { get; private set; }         // woodpanel_trophys
        public static Sprite PanelBkg { get; private set; }      // panel_bkg
        public static Sprite PanelWood { get; private set; }     // woodpanel_400_tileable
        public static Sprite Button { get; private set; }
        public static Sprite ButtonHighlight { get; private set; }
        public static Sprite ButtonPressed { get; private set; }
        public static Sprite TextField { get; private set; }
        public static Sprite ItemBackground { get; private set; }
        public static Sprite Sunken { get; private set; }

        /// <summary>A white rounded square for the islands (radius 12), drawn sliced. Made at runtime, never written to disk.</summary>
        public static Sprite Round { get; private set; }

        /// <summary>The same with a small radius (6): buttons, fields, chips.</summary>
        public static Sprite RoundSmall { get; private set; }

        /// <summary>Goes up on every rebuild, so anything built from the theme can notice.</summary>
        public static int Generation { get; private set; }

        public static bool Ready => _font != null;

        /// <summary>Switches a text material's edge or shadow off: the shaders read the alpha.</summary>
        private static readonly Color NoColour = new Color(0f, 0f, 0f, 0f);

        /// <summary>The black edge under white text over the picture. Wide enough to carry a sky.</summary>
        private const float OutlineWidth = 0.2f;

        private static Hud _builtFrom;
        private static readonly List<Sprite> Copies = new List<Sprite>();
        private static readonly List<Object> Made = new List<Object>();

        /// <summary>True once the font and sprites are cached for the current world.</summary>
        public static bool Ensure()
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_hoverName == null)
            {
                return false;
            }

            if (_font != null && _builtFrom == hud)
            {
                return true;
            }

            Clear();

            _gameFont = hud.m_hoverName.font;
            var gameSource = hud.m_hoverName.fontSharedMaterial;
            _gameMaterial = OwnTextMaterial(gameSource, "ValheimTomrerGameText", NoColour, 0f, false);
            _gameOutlined = OwnTextMaterial(gameSource, "ValheimTomrerGameTextOutlined", Color.black, OutlineWidth, true);

            // The editor's own font: a plain sans when the game has one loaded, with its own material
            // (a material belongs to its font's atlas, so the HUD's cannot be used with another font).
            var sans = PickSans();
            if (sans != null && sans.material != null)
            {
                _font = sans;
                _fontMaterial = OwnTextMaterial(sans.material, "ValheimTomrerText", NoColour, 0f, false);
                _fontOutlined = OwnTextMaterial(sans.material, "ValheimTomrerTextOutlined", Color.black, OutlineWidth, true);
            }
            else
            {
                _font = _gameFont;
                _fontMaterial = OwnTextMaterial(gameSource, "ValheimTomrerText", NoColour, 0f, false);
                _fontOutlined = OwnTextMaterial(gameSource, "ValheimTomrerTextOutlined", Color.black, OutlineWidth, true);
            }

            var atlas = Resources.FindObjectsOfTypeAll<SpriteAtlas>().FirstOrDefault(a => a.name == "UIAtlas");
            if (atlas == null)
            {
                ValheimTomrerPlugin.Log.LogWarning("UIAtlas not found: the editor window will draw without chrome.");
            }

            Panel = Take(atlas, "woodpanel_trophys");
            PanelBkg = Take(atlas, "panel_bkg");
            PanelWood = Take(atlas, "woodpanel_400_tileable");
            Button = Take(atlas, "button");
            ButtonHighlight = Take(atlas, "button_highlight");
            ButtonPressed = Take(atlas, "button_pressed");
            TextField = Take(atlas, "text_field");
            ItemBackground = Take(atlas, "item_background");
            Sunken = Take(atlas, "sunken");

            Round = MakeRound("ValheimTomrerRound", 12);
            RoundSmall = MakeRound("ValheimTomrerRoundSmall", 6);

            _builtFrom = hud;
            Generation++;
            ValheimTomrerPlugin.Log.LogInfo(
                $"editor theme ready | font={(_font != null ? _font.name : "none")} | game font={(_gameFont != null ? _gameFont.name : "none")} | sprites={Copies.Count}/9");
            return _font != null;
        }

        public static void Clear()
        {
            foreach (var copy in Copies)
            {
                if (copy != null)
                {
                    Object.Destroy(copy);
                }
            }

            Copies.Clear();
            foreach (var made in Made)
            {
                if (made != null)
                {
                    Object.Destroy(made);
                }
            }

            Made.Clear();
            Round = RoundSmall = null;
            PadGlyphs.Clear();
            foreach (var material in new[] { _fontMaterial, _fontOutlined, _gameMaterial, _gameOutlined })
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }

            _font = null;
            _gameFont = null;
            _fontMaterial = _fontOutlined = _gameMaterial = _gameOutlined = null;
            Panel = PanelBkg = PanelWood = Button = ButtonHighlight = ButtonPressed = TextField
                = ItemBackground = Sunken = null;
            _builtFrom = null;
        }

        /// <summary>Plain sans fonts, best first. The first one the game has loaded wins.</summary>
        private static readonly string[] SansNames =
        {
            "LiberationSans", "Roboto", "Inter", "OpenSans", "NotoSans", "Arial", "AveriaSansLibre",
        };

        private static TMP_FontAsset PickSans()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            ValheimTomrerPlugin.Log.LogInfo("editor theme: fonts loaded: " + string.Join(", ", fonts.Select(f => f.name).Distinct()));
            foreach (var name in SansNames)
            {
                var font = fonts.FirstOrDefault(f => f != null && f.material != null
                    && f.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (font != null)
                {
                    return font;
                }
            }

            return null;
        }

        /// <summary>
        /// The editor's own copies of the HUD's text material.
        ///
        /// TMP multiplies the label's colour by the material's face colour and then draws the
        /// material's outline and shadow over the glyph. The hover-name material the HUD uses is
        /// tuned for white names over a dark world: a fat black outline and a soft shadow. At the
        /// 12 to 16 point sizes the panels use, that eats the strokes and every label reads grey
        /// however light its colour is.
        ///
        /// So there are two of our own, all with a white face at full strength so the label's
        /// colour is the only thing deciding how it looks:
        /// <list type="bullet">
        /// <item><see cref="FontMaterial"/>: no edge and no shadow, for text on a panel, where the
        /// wood behind it is dark and known.</item>
        /// <item><see cref="FontOutlined"/>: a black edge and a shadow, for white text over the
        /// 3D picture, where the background can be as light as the sky.</item>
        /// </list>
        /// Both are copies of a loaded material, made at runtime. Nothing is written to disk.
        /// </summary>
        private static Material OwnTextMaterial(Material source, string name, Color edge, float edgeWidth, bool shadow)
        {
            if (source == null)
            {
                return null;
            }

            var mine = new Material(source) { name = name };
            Set(mine, ShaderUtilities.ID_FaceColor, Color.white);
            Set(mine, ShaderUtilities.ID_GlowColor, NoColour);
            Set(mine, ShaderUtilities.ID_GlowPower, 0f);
            mine.DisableKeyword(ShaderUtilities.Keyword_Glow);

            // A fat edge eats into the glyph, so the face is dilated back out.
            var edged = edge.a > 0f && edgeWidth > 0f;
            Set(mine, ShaderUtilities.ID_FaceDilate, edgeWidth >= 0.15f ? 0.1f : 0.05f);
            Set(mine, ShaderUtilities.ID_OutlineColor, edged ? edge : NoColour);
            Set(mine, ShaderUtilities.ID_OutlineWidth, edged ? edgeWidth : 0f);
            Set(mine, ShaderUtilities.ID_OutlineSoftness, 0f);
            Set(mine, ShaderUtilities.ID_UnderlayColor, shadow ? new Color(0f, 0f, 0f, 0.65f) : NoColour);
            Set(mine, ShaderUtilities.ID_UnderlayOffsetX, shadow ? 0.5f : 0f);
            Set(mine, ShaderUtilities.ID_UnderlayOffsetY, shadow ? -0.5f : 0f);
            Set(mine, ShaderUtilities.ID_UnderlayDilate, shadow ? 0.1f : 0f);
            Set(mine, ShaderUtilities.ID_UnderlaySoftness, shadow ? 0.2f : 0f);
            Keyword(mine, ShaderUtilities.Keyword_Outline, edged);
            Keyword(mine, ShaderUtilities.Keyword_Underlay, shadow);
            return mine;
        }

        private static void Keyword(Material material, string keyword, bool on)
        {
            if (on)
            {
                material.EnableKeyword(keyword);
                return;
            }

            material.DisableKeyword(keyword);
        }

        /// <summary>The distance-field shaders come in variants, so a property can be missing.</summary>
        private static void Set(Material material, int id, float value)
        {
            if (material.HasProperty(id))
            {
                material.SetFloat(id, value);
            }
        }

        private static void Set(Material material, int id, Color value)
        {
            if (material.HasProperty(id))
            {
                material.SetColor(id, value);
            }
        }

        /// <summary>
        /// A white square with rounded corners, a few pixels big, in memory only: sliced by an Image it
        /// gives any size a round corner of the same radius (50 pixels per unit, so one pixel is one
        /// canvas unit). The art rule: it is made from code at runtime, nothing is saved.
        /// </summary>
        private static Sprite MakeRound(string name, int radius)
        {
            var size = (radius * 2) + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                    var dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                    var edge = Mathf.Clamp01(radius - Mathf.Sqrt((dx * dx) + (dy * dy)) + 0.5f);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(edge * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                50f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = name;
            Made.Add(texture);
            Made.Add(sprite);
            return sprite;
        }

        private static Sprite Take(SpriteAtlas atlas, string name)
        {
            if (atlas == null)
            {
                return null;
            }

            var sprite = atlas.GetSprite(name);
            if (sprite == null)
            {
                ValheimTomrerPlugin.Log.LogWarning($"UIAtlas has no sprite '{name}'.");
                return null;
            }

            // GetSprite hands out a copy, not the atlas entry, so it is ours to destroy.
            Copies.Add(sprite);
            return sprite;
        }
    }
}
