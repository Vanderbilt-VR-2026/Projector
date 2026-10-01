using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Draws the projected 2D game on the projection stage (the plane z = 0 of this transform, filmed by the
    // projector camera): flat colored shapes with a dark border, the look of a 2D game rather than a shadow.
    // While building it shows a live preview of the props; on PROJECT the shapes become solid platforms
    // with a coin over each. Start and finish platforms are always there.
    public class ProjectedPlatforms : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material shapeMaterial;
        [SerializeField] Material coinMaterial;
        [SerializeField] float depth = 1.5f;
        [SerializeField] float borderWidth = 0.12f;
        [Tooltip("Always-present platforms in level coordinates (start, finish).")]
        [SerializeField] Rect[] fixedPlatforms = { new Rect(-10f, 1f, 3f, 1f), new Rect(7f, 1f, 3f, 1f) };
        [SerializeField] Color[] fixedColors = { new Color(0.3f, 0.75f, 0.4f), new Color(0.95f, 0.72f, 0.2f) };

        readonly List<Shape> preview = new List<Shape>();
        Transform previewRoot;
        Transform levelRoot;

        public int LevelPlatformCount { get; private set; }
        public int PreviewCount { get; private set; }

        class Shape
        {
            public GameObject Root;
            public Mesh Fill;
            public Mesh Border;
            public MeshRenderer FillRenderer;
            public MeshRenderer BorderRenderer;
        }

        void Awake()
        {
            previewRoot = new GameObject("Preview").transform;
            previewRoot.SetParent(transform, false);
            levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(transform, false);

            var fixedRoot = new GameObject("Start and Finish").transform;
            fixedRoot.SetParent(transform, false);
            for (var i = 0; i < fixedPlatforms.Length; i++)
            {
                var rect = fixedPlatforms[i];
                var shape = CreateShape("Fixed Platform " + (i + 1), fixedRoot);
                Draw(shape, Rectangle(rect), fixedColors[i % fixedColors.Length]);
                var box = shape.Root.AddComponent<BoxCollider>();
                box.center = new Vector3(rect.center.x, rect.center.y, 0f);
                box.size = new Vector3(rect.width, rect.height, depth);
            }
        }

        public void SetPreviewVisible(bool visible) => previewRoot.gameObject.SetActive(visible);

        // Redraws the live preview, reusing meshes so it can run every few frames.
        public void ShowPreview(IReadOnlyList<ProjectedShape> shapes)
        {
            while (preview.Count < shapes.Count)
                preview.Add(CreateShape("Preview " + (preview.Count + 1), previewRoot));
            for (var i = 0; i < preview.Count; i++)
            {
                var active = i < shapes.Count;
                preview[i].Root.SetActive(active);
                if (active)
                    Draw(preview[i], shapes[i].Outline, shapes[i].Color);
            }
            PreviewCount = shapes.Count;
        }

        public void BuildLevel(IReadOnlyList<ProjectedShape> shapes)
        {
            ClearLevel();
            for (var i = 0; i < shapes.Count; i++)
            {
                var shape = CreateShape("Platform " + (i + 1), levelRoot);
                Draw(shape, shapes[i].Outline, shapes[i].Color);
                var collider = shape.Root.AddComponent<MeshCollider>();
                collider.sharedMesh = Extrude(shapes[i].Outline, depth);
                collider.convex = true;
                CreateCoin(i, shapes[i].Outline);
            }
            LevelPlatformCount = shapes.Count;
        }

        public void ClearLevel()
        {
            for (var i = levelRoot.childCount - 1; i >= 0; i--)
                Destroy(levelRoot.GetChild(i).gameObject);
            LevelPlatformCount = 0;
        }

        Shape CreateShape(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var shape = new Shape { Root = root, Fill = new Mesh { name = name + " Fill" }, Border = new Mesh { name = name + " Border" } };
            shape.FillRenderer = AddMesh(root.transform, "Fill", shape.Fill, 0f);
            // The border sits just behind the fill (farther from the projector camera).
            shape.BorderRenderer = AddMesh(root.transform, "Border", shape.Border, 0.05f);
            return shape;
        }

        MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = shapeMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return meshRenderer;
        }

        // Flat 2D styling: the prop's color, a touch brighter, inside a dark border of the same hue.
        void Draw(Shape shape, Vector2[] outline, Color color)
        {
            Fan(shape.Fill, outline);
            Fan(shape.Border, ProjectedLevel.Expand(outline, borderWidth));
            Tint(shape.FillRenderer, Color.Lerp(color, Color.white, 0.15f));
            Tint(shape.BorderRenderer, color * 0.3f);
        }

        static void Tint(Renderer target, Color color)
        {
            var block = new MaterialPropertyBlock();
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }

        void CreateCoin(int index, Vector2[] outline)
        {
            var top = outline[0];
            var center = Vector2.zero;
            foreach (var point in outline)
            {
                center += point;
                if (point.y > top.y)
                    top = point;
            }
            center /= outline.Length;

            var coin = new GameObject("Coin " + (index + 1));
            coin.transform.SetParent(levelRoot, false);
            coin.transform.localPosition = new Vector3(center.x, top.y + 0.6f, 0f);
            var face = CreateDisc(coin.transform, "Face", 0.32f, 0f, new Color(1f, 0.82f, 0.2f));
            CreateDisc(coin.transform, "Rim", 0.4f, 0.04f, new Color(0.45f, 0.3f, 0.05f));
            var trigger = coin.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.3f;
            coin.AddComponent<Coin>();
        }

        GameObject CreateDisc(Transform parent, string name, float diameter, float z, Color color)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0f, z);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            var discRenderer = disc.GetComponent<MeshRenderer>();
            discRenderer.sharedMaterial = coinMaterial;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(discRenderer, color);
            return disc;
        }

        static Vector2[] Rectangle(Rect rect)
        {
            return new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
        }

        // A flat polygon facing the projector camera (which looks along +z).
        static void Fan(Mesh mesh, Vector2[] outline)
        {
            var vertices = new Vector3[outline.Length];
            for (var i = 0; i < outline.Length; i++)
                vertices[i] = outline[i];
            var triangles = new int[(outline.Length - 2) * 3];
            for (var i = 1; i < outline.Length - 1; i++)
            {
                triangles[(i - 1) * 3] = 0;
                triangles[(i - 1) * 3 + 1] = i + 1;
                triangles[(i - 1) * 3 + 2] = i;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        // Solid prism from a counter-clockwise convex outline, for the platform's collider (convex cooking
        // only needs the corner points; the caps keep the mesh valid).
        static Mesh Extrude(Vector2[] outline, float depth)
        {
            var n = outline.Length;
            var vertices = new Vector3[n * 2];
            for (var i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(outline[i].x, outline[i].y, -depth / 2f);
                vertices[n + i] = new Vector3(outline[i].x, outline[i].y, depth / 2f);
            }
            var triangles = new List<int>();
            for (var i = 1; i < n - 1; i++)
            {
                triangles.AddRange(new[] { 0, i + 1, i });
                triangles.AddRange(new[] { n, n + i, n + i + 1 });
            }
            var mesh = new Mesh { name = "Platform Collider", vertices = vertices };
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
