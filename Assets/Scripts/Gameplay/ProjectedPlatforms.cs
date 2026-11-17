using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Draws the projected 2D game on the projection stage (the plane z = 0 of this transform, filmed by the
    // projector camera): each prop as the projector renders it, its faces in its own colors with the corners
    // and edges inked in, the look of a 2D game rather than a shadow.
    // While building it shows a live preview of the props; on PROJECT the drawings become the level:
    // hazards send runners back to the start, bounce pads launch them, and only coin items become pickups.
    // Start and finish platforms are always there.
    public class ProjectedPlatforms : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material shapeMaterial;
        [SerializeField] Material coinMaterial;
        [Tooltip("Vertex-colored material for the props' drawings (Projector/Stage Drawing).")]
        [SerializeField] Material drawingMaterial;
        [SerializeField] float depth = 1.5f;
        [Tooltip("Always-present platforms in level coordinates (start, finish), their tops at y = 2.")]
        [SerializeField] Rect[] fixedPlatforms = { new Rect(-14.25f, 1.5f, 1.5f, 0.5f), new Rect(12.75f, 1.5f, 1.5f, 0.5f) };
        [SerializeField] Color[] fixedColors = { new Color(0.3f, 0.75f, 0.4f), new Color(0.95f, 0.72f, 0.2f) };

        readonly List<Drawing> preview = new List<Drawing>();
        Transform previewRoot;
        Transform levelRoot;

        public int LevelPlatformCount { get; private set; }
        public int PreviewCount { get; private set; }

        class Drawing
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
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
                var platform = CreateDrawing("Fixed Platform " + (i + 1), fixedRoot, shapeMaterial);
                Fan(platform.Mesh, Rectangle(rect));
                Tint(platform.Renderer, fixedColors[i % fixedColors.Length]);
                var box = platform.Root.AddComponent<BoxCollider>();
                box.center = new Vector3(rect.center.x, rect.center.y, 0f);
                box.size = new Vector3(rect.width, rect.height, depth);
            }
        }

        public void SetPreviewVisible(bool visible) => previewRoot.gameObject.SetActive(visible);

        // Redraws the live preview, reusing meshes so it can run every few frames.
        public void ShowPreview(IReadOnlyList<ProjectedItem> items)
        {
            while (preview.Count < items.Count)
                preview.Add(CreateDrawing("Preview " + (preview.Count + 1), previewRoot, drawingMaterial));
            for (var i = 0; i < preview.Count; i++)
            {
                var active = i < items.Count;
                preview[i].Root.SetActive(active);
                if (active)
                    items[i].CopyTo(preview[i].Mesh);
            }
            PreviewCount = items.Count;
        }

        public void BuildLevel(IReadOnlyList<ProjectedItem> items)
        {
            ClearLevel();
            var solid = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // A coin item doesn't become ground: it turns into a pickup where it was drawn.
                if (item.Kind == ShapeKind.Coin)
                {
                    CreateCoinAt("Coin Item " + (i + 1), item.Center);
                    continue;
                }

                var drawing = CreateDrawing(item.Kind + " " + (i + 1), levelRoot, drawingMaterial);
                item.CopyTo(drawing.Mesh);
                // One convex collider per part, so the item is solid in its own shape (an L stays an L).
                for (var p = 0; p < item.Parts.Count; p++)
                {
                    var part = new GameObject("Part " + (p + 1));
                    part.transform.SetParent(drawing.Root.transform, false);
                    var collider = part.AddComponent<MeshCollider>();
                    collider.sharedMesh = Extrude(item.Parts[p], depth);
                    collider.convex = true;
                }
                if (item.Parts.Count == 0)
                    continue;
                solid++;

                switch (item.Kind)
                {
                    case ShapeKind.Hazard:
                        drawing.Root.AddComponent<Hazard>().Setup(drawing.Renderer);
                        break;
                    case ShapeKind.Bounce:
                        drawing.Root.AddComponent<BouncePad>().Setup(drawing.Renderer);
                        break;
                }
            }
            LevelPlatformCount = solid;
        }

        public void ClearLevel()
        {
            for (var i = levelRoot.childCount - 1; i >= 0; i--)
                Destroy(levelRoot.GetChild(i).gameObject);
            LevelPlatformCount = 0;
        }

        Drawing CreateDrawing(string name, Transform parent, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Drawing { Root = root, Mesh = mesh, Renderer = meshRenderer };
        }

        static void Tint(Renderer target, Color color)
        {
            var block = new MaterialPropertyBlock();
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }

        void CreateCoinAt(string name, Vector2 position)
        {
            var coin = new GameObject(name);
            coin.transform.SetParent(levelRoot, false);
            coin.transform.localPosition = new Vector3(position.x, position.y, 0f);
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
