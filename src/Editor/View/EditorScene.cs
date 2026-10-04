using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimTomrer.Editor.Ui;
using UnityEngine.Rendering;

namespace ValheimTomrer.Editor.View
{
    /// <summary>
    /// The little world the editor draws: a ground plane with a metre grid, a ring on the origin, a
    /// red marker on the blueprint's front, and its own two lights. It sits 8000 m below the world
    /// and everything in it is on the editor layer, so the player can neither see it nor walk into it.
    ///
    /// Every mesh and material is made here at runtime and destroyed with the scene. Nothing is
    /// loaded from disk.
    /// </summary>
    internal sealed class EditorScene
    {
        /// <summary>How far under the world the scene hangs.</summary>
        public const float Depth = -8000f;

        private const float GroundSize = 800f;
        private const int GridCells = 100;       // 1 m cells, 100 x 100
        private const float GridY = 0.003f;
        private const float RingY = 0.006f;

        // The scene's colours: UiTheme.Scene*.
        private static readonly Color FrontColor = Hex(0xD9412B);
        private static readonly Color AxisXColor = new Color(0.90f, 0.30f, 0.30f, 0.85f);
        private static readonly Color AxisZColor = new Color(0.30f, 0.55f, 1f, 0.85f);

        private Material _groundMaterial;
        private Material _gridMaterial;
        private Material _majorMaterial;
        private Material _ringMaterial;

        private readonly int _layer;
        private readonly GameObject _root;
        private readonly List<Object> _owned = new List<Object>();   // meshes and materials, ours to destroy
        private readonly GameObject _front;
        private readonly Transform _chevron;
        private readonly Transform _dot;

        public EditorScene(int layer)
        {
            _layer = layer;
            _root = new GameObject("ValheimTomrer_EditorScene") { layer = layer };
            _root.transform.position = new Vector3(0f, Depth, 0f);

            var lit = Shading.Lit();
            var unlit = Shading.Unlit();

            _groundMaterial = Paint(lit, UiTheme.SceneGround);
            Matte(_groundMaterial);
            _gridMaterial = Paint(unlit, UiTheme.SceneGrid);
            _majorMaterial = Paint(unlit, UiTheme.SceneGridMajor);
            _ringMaterial = Paint(unlit, UiTheme.SceneRing);
            var ground = Add("Ground", Quad(GroundSize), _groundMaterial, 0f);
            GroundCollider = ground.gameObject.AddComponent<BoxCollider>();
            GroundCollider.size = new Vector3(GroundSize, 0.02f, GroundSize);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.receiveShadows = true;
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // A 1 m grid with every fifth line brighter, so distances can be counted, and the two ground
            // axes in the gizmo's colours (X red, Z blue).
            _lines.Add(Add("Grid", Grid(GridCells, 5, true), _gridMaterial, GridY));
            _lines.Add(Add("GridMajor", Grid(GridCells, 5, false), _majorMaterial, GridY + 0.001f));
            _lines.Add(Add("AxisX", Axis(true), Paint(unlit, AxisXColor), GridY + 0.002f));
            _lines.Add(Add("AxisZ", Axis(false), Paint(unlit, AxisZColor), GridY + 0.002f));
            Add("Origin", Ring(0.12f, 0.2f, 32), _ringMaterial, RingY);

            _front = new GameObject("Front") { layer = layer };
            _front.transform.SetParent(_root.transform, false);
            _front.SetActive(false);
            var frontMaterial = Paint(unlit, FrontColor);
            _chevron = Add("Chevron", Chevron(), frontMaterial, 0f, _front.transform);
            _dot = Add("Anchor", Disc(0.09f, 16), frontMaterial, 0f, _front.transform);

            _key = AddLight("Key", Quaternion.identity, Color.white, 1f, true);
            _fill = AddLight("Fill", Quaternion.identity, Color.white, 0.3f, false);
            ApplyLook(SceneLook.Now);
        }

        public Transform Root => _root != null ? _root.transform : null;

        private readonly List<Transform> _lines = new List<Transform>();

        /// <summary>The grid lines and the axes are drawn (the ground and the origin ring always are).</summary>
        public bool GridVisible { get; private set; } = true;

        /// <summary>Shows or hides the grid lines and the axes.</summary>
        public void SetGridVisible(bool on)
        {
            if (on == GridVisible)
            {
                return;
            }

            GridVisible = on;
            foreach (var part in _lines)
            {
                if (part != null)
                {
                    part.gameObject.SetActive(on);
                }
            }
        }

        public BoxCollider GroundCollider { get; }

        public int Layer => _layer;

        public bool IsAlive => _root != null;

        /// <summary>
        /// The blueprint's front (+Z, the side that faces the player when the mod places it): a flat
        /// arrow on the ground in front of it, and a dot on the point the player aims at.
        /// </summary>
        public void SetFront(Bounds bounds)
        {
            if (_front == null)
            {
                return;
            }

            var anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.max.z);
            var length = Mathf.Clamp(bounds.size.x * 0.25f, 0.9f, 2.5f);
            var y = Mathf.Max(anchor.y, 0f) + 0.015f;

            _front.SetActive(true);
            _chevron.localPosition = new Vector3(anchor.x, y, anchor.z + 0.25f);
            _chevron.localScale = new Vector3(length, 1f, length);
            _dot.localPosition = new Vector3(anchor.x, y + 0.005f, anchor.z);
        }

        public void HideFront()
        {
            if (_front != null)
            {
                _front.SetActive(false);
            }
        }

        /// <summary>Meshes and materials still alive. The autotest reads it after closing: it must be 0.</summary>
        public int AliveOwned()
        {
            return _owned.Count(o => o != null);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            foreach (var owned in _owned)
            {
                if (owned != null)
                {
                    Object.Destroy(owned);
                }
            }
        }

        // ---------- building blocks ----------

        private Transform Add(string name, Mesh mesh, Material material, float y, Transform parent = null)
        {
            var go = new GameObject(name) { layer = _layer };
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private UnityEngine.Light _key;
        private UnityEngine.Light _fill;
        private TimeOfDay? _look;

        /// <summary>
        /// The light of a time of day: the sun's place and colour, the cool fill, the shadows' strength, and the
        /// colours of the ground and the grid. A no-op when it is already that one.
        /// </summary>
        public void ApplyLook(TimeOfDay time)
        {
            if (_look == time || _key == null)
            {
                return;
            }

            _look = time;
            var look = SceneLook.Of(time);
            _key.transform.localRotation = Quaternion.Euler(look.Elevation, look.Azimuth, 0f);
            _key.color = look.KeyColor;
            _key.intensity = look.KeyIntensity;
            _key.shadowStrength = look.ShadowStrength;
            _fill.transform.localRotation = Quaternion.Euler(-15f, look.Azimuth + 160f, 0f);
            _fill.color = look.FillColor;
            _fill.intensity = look.FillIntensity;
            Tint(_groundMaterial, look.Ground);
            Tint(_gridMaterial, look.Grid);
            Tint(_majorMaterial, look.GridMajor);
            Tint(_ringMaterial, look.Ring);
        }

        private static void Tint(Material material, Color color)
        {
            if (material != null)
            {
                material.color = color;
            }
        }

        private UnityEngine.Light AddLight(string name, Quaternion rotation, Color color, float intensity, bool shadows)
        {
            var go = new GameObject(name) { layer = _layer };
            go.transform.SetParent(_root.transform, false);
            go.transform.localRotation = rotation;
            var light = go.AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.cullingMask = 1 << _layer;   // lights our scene only, never the player's world
            return light;
        }

        private Material Paint(Shader shader, Color color)
        {
            var material = new Material(shader) { name = "ValheimTomrer_Editor", color = color };
            _owned.Add(material);
            return material;
        }

        private Mesh Take(Mesh mesh)
        {
            mesh.name = "ValheimTomrer_Editor";
            _owned.Add(mesh);
            return mesh;
        }

        private Mesh Quad(float size)
        {
            var half = size * 0.5f;
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half),
                    new Vector3(half, 0f, half), new Vector3(half, 0f, -half),
                },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            return Take(mesh);
        }

        /// <summary>A line mesh: one metre per cell, drawn as lines so it stays one draw call.</summary>
        /// <summary>
        /// The grid's lines on the ground, 1 m apart. With <paramref name="skipEvery"/> the lines on every
        /// <paramref name="every"/>-th metre are left out (they are the other mesh's); without it only those are drawn.
        /// </summary>
        private Mesh Grid(int cells, int every, bool skipEvery)
        {
            var half = cells * 0.5f;
            var vertices = new List<Vector3>((cells + 1) * 4);
            var indices = new List<int>((cells + 1) * 4);
            for (var i = 0; i <= cells; i++)
            {
                var onMajor = i % every == 0;
                if (onMajor == skipEvery)
                {
                    continue;
                }

                var at = -half + i;
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(at, 0f, -half));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(at, 0f, half));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(-half, 0f, at));
                indices.Add(vertices.Count);
                vertices.Add(new Vector3(half, 0f, at));
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return Take(mesh);
        }

        /// <summary>One line through the origin along the X or the Z axis, the length of the grid.</summary>
        private Mesh Axis(bool x)
        {
            var half = GridCells * 0.5f;
            var mesh = new Mesh();
            mesh.SetVertices(new List<Vector3>
            {
                x ? new Vector3(-half, 0f, 0f) : new Vector3(0f, 0f, -half),
                x ? new Vector3(half, 0f, 0f) : new Vector3(0f, 0f, half),
            });
            mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return Take(mesh);
        }

        /// <summary>No shine: the standard shader gives a glossy sheen that reflects the world's sky and washes the ground out.</summary>
        private static void Matte(Material material)
        {
            foreach (var property in new[] { "_Glossiness", "_Metallic", "_Smoothness", "_SpecularHighlights", "_GlossyReflections" })
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, 0f);
                }
            }

            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        }

        private Mesh Ring(float inner, float outer, int segments)
        {
            var vertices = new List<Vector3>(segments * 2);
            var triangles = new List<int>(segments * 6);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(dir * inner);
                vertices.Add(dir * outer);
            }

            for (var i = 0; i < segments; i++)
            {
                var a = i * 2;
                var b = (i * 2 + 2) % (segments * 2);
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }

            return Take(Flat(vertices, triangles));
        }

        private Mesh Disc(float radius, int segments)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>(segments * 3);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            for (var i = 0; i < segments; i++)
            {
                triangles.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % segments });
            }

            return Take(Flat(vertices, triangles));
        }

        /// <summary>The arrow that marks the front, one metre long: scale it to the blueprint's width.</summary>
        private Mesh Chevron()
        {
            const float w = 0.28f;
            var vertices = new List<Vector3>
            {
                new Vector3(-w * 0.45f, 0f, 0f),
                new Vector3(w * 0.45f, 0f, 0f),
                new Vector3(w * 0.45f, 0f, 0.55f),
                new Vector3(w, 0f, 0.55f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-w, 0f, 0.55f),
                new Vector3(-w * 0.45f, 0f, 0.55f),
            };
            var triangles = new List<int> { 0, 1, 2, 0, 2, 6, 5, 4, 3 };
            return Take(Flat(vertices, triangles));
        }

        /// <summary>A flat mesh lying on the ground, drawn from both sides so the winding cannot hide it.</summary>
        private static Mesh Flat(List<Vector3> vertices, List<int> triangles)
        {
            var both = new List<int>(triangles);
            for (var i = 0; i < triangles.Count; i += 3)
            {
                both.Add(triangles[i]);
                both.Add(triangles[i + 2]);
                both.Add(triangles[i + 1]);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(both, 0);
            mesh.SetNormals(vertices.Select(_ => Vector3.up).ToList());
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }
    }

    /// <summary>
    /// Shaders for the editor's own ground and markers. A built game only has the shaders it
    /// already loaded, so the name is looked up among those instead of assumed.
    /// </summary>
    internal static class Shading
    {
        private static Shader _lit;
        private static Shader _unlit;
        private static Shader _onTop;

        public static Shader Lit()
        {
            return _lit != null
                ? _lit
                : _lit = Find("lit", "Standard", "Legacy Shaders/Diffuse", "Sprites/Default", "Unlit/Color");
        }

        public static Shader Unlit()
        {
            return _unlit != null
                ? _unlit
                : _unlit = Find("unlit", "Sprites/Default", "Unlit/Color", "Legacy Shaders/Particles/Alpha Blended",
                    "Hidden/Internal-Colored", "UI/Default");
        }

        /// <summary>
        /// A shader whose depth test can be switched off. Sprites/Default has none, so a box behind
        /// a wall would be hidden; these three all take the test from a property.
        /// </summary>
        public static Shader OnTop()
        {
            return _onTop != null
                ? _onTop
                : _onTop = Find("on top", "Hidden/Internal-Colored", "UI/Default", "Sprites/Default", "Unlit/Color");
        }

        /// <summary>
        /// A flat colour with no lighting. With <paramref name="onTop"/> it also ignores the depth
        /// buffer, so selection boxes and snap dots stay readable through the pieces. The caller
        /// owns the material and has to destroy it.
        /// </summary>
        public static Material Overlay(Color color, bool onTop)
        {
            var material = new Material(onTop ? OnTop() : Unlit()) { name = "ValheimTomrer_Editor", color = color };
            if (!onTop)
            {
                return material;
            }

            // Whichever of the three it landed on, one of these names is the one it reads.
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 4000;
            return material;
        }

        private static Shader Find(string what, params string[] names)
        {
            foreach (var name in names)
            {
                var shader = Shader.Find(name) ?? Resources.FindObjectsOfTypeAll<Shader>()
                    .FirstOrDefault(s => s.name == name);
                if (shader != null)
                {
                    ValheimTomrerPlugin.Log.LogInfo($"editor {what} shader: {shader.name}");
                    return shader;
                }
            }

            var any = Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name.Contains("Unlit"));
            ValheimTomrerPlugin.Log.LogWarning($"editor {what} shader: none of {string.Join(", ", names)} is loaded,"
                + $" falling back to {(any != null ? any.name : "nothing")}");
            return any;
        }
    }

    /// <summary>
    /// A mesh the editor rebuilds in place whenever what it draws changes: one object, one
    /// material, one draw call. The selection boxes and the snap dots are both made of these.
    /// </summary>
    internal sealed class OverlayMesh
    {
        private readonly GameObject _object;
        private readonly Mesh _mesh;
        private readonly Material _material;

        public OverlayMesh(string name, Transform parent, int layer, Color color)
        {
            _object = new GameObject(name) { layer = layer };
            _object.transform.SetParent(parent, false);
            _mesh = new Mesh { name = "ValheimTomrer_" + name, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = Shading.Overlay(color, true);
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _object.SetActive(false);
        }

        public void Set(List<Vector3> vertices, List<int> triangles)
        {
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            if (_object != null && !_object.activeSelf)
            {
                _object.SetActive(true);
            }
        }

        public void Clear()
        {
            if (_object != null && _object.activeSelf)
            {
                _mesh.Clear();
                _object.SetActive(false);
            }
        }

        public void Destroy()
        {
            if (_object != null)
            {
                Object.Destroy(_object);
            }

            if (_mesh != null)
            {
                Object.Destroy(_mesh);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }
    }
}
