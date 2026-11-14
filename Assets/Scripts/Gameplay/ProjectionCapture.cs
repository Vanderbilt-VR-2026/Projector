using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Sits at the projector lens and draws what the projector sees of each prop, the way the prop would look to
    // someone looking from the lens. Every face of the prop's meshes that faces the lens is projected from the
    // lens onto the projection wall (a plane of constant x) and lit in its part's own color and finish. Faces
    // hidden behind nearer ones are covered by them on the stage. The screen rectangle on that wall maps onto the
    // 2D level, so each prop is drawn exactly where it sits in the beam. Each part's projected outline also
    // becomes a collider, so an L-block stays an L and a spike strip keeps its spikes.
    public class ProjectionCapture : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        // Closest a point can be to the lens (along the beam) and still be drawn.
        const float MinDepth = 0.05f;
        // Stage z range the drawings use (the stage camera looks along +z); runners and coins at z = 0 stay in
        // front of them.
        const float StageNear = 0.5f;
        const float StageFar = 4f;

        [Tooltip("x of the projection wall's inner face.")]
        [SerializeField] float wallX = -3.92f;
        [Tooltip("Screen rectangle on the wall: x = z range, y = height range. Maps onto levelMin..levelMax.")]
        [SerializeField] Rect screen = new Rect(-2.3f, 1.1f, 18f, 6f);
        [SerializeField] Vector2 levelMin = new Vector2(-15f, 0f);
        [SerializeField] Vector2 levelMax = new Vector2(15f, 10f);
        [Tooltip("Parts smaller than this on screen (level units squared) get no collider.")]
        [SerializeField] float minimumPartArea = 0.004f;

        [Header("Lighting")]
        [Tooltip("Where the main light hangs, from the lens (world axes). Being close, it lights each face a little unevenly, as a real lamp does.")]
        [SerializeField] Vector3 lightOffset = new Vector3(0.3f, 1.6f, -0.8f);
        [SerializeField, Range(0f, 2f)] float lightStrength = 0.8f;
        [Tooltip("How quickly the main light fades with distance.")]
        [SerializeField, Min(0f)] float lightFalloff = 0.12f;
        [Tooltip("Soft light from all around: the first color on faces turned up, the second on faces turned down.")]
        [SerializeField] Color skyLight = new Color(0.56f, 0.56f, 0.58f);
        [SerializeField] Color groundLight = new Color(0.34f, 0.32f, 0.3f);
        [Tooltip("Light from the projector itself, on the faces turned toward the lens.")]
        [SerializeField, Range(0f, 1f)] float lensLight = 0.2f;
        [Tooltip("Strength of the highlights on smooth and metal parts.")]
        [SerializeField, Range(0f, 1f)] float highlights = 0.35f;

        public Rect Screen => screen;
        public float WallX => wallX;
        public Vector2 LevelMin => levelMin;
        public Vector2 LevelMax => levelMax;

        // A part's material, as the lighting needs it.
        struct Finish
        {
            public Color Color;
            public float Smoothness;
            public float Metallic;
        }

        readonly List<Transform> props = new List<Transform>();
        readonly List<ProjectedItem> pool = new List<ProjectedItem>();
        readonly List<MeshFilter> filters = new List<MeshFilter>();
        readonly List<Vector3> meshVertices = new List<Vector3>();
        readonly List<Vector3> meshNormals = new List<Vector3>();
        readonly List<int> meshTriangles = new List<int>();
        readonly List<Vector2> hullPoints = new List<Vector2>();
        Vector3[] world = new Vector3[0];
        Vector3[] projected = new Vector3[0];
        Color[] lit = new Color[0];
        bool[] valid = new bool[0];

        // Every prop the players have handled, and any other prop in the room (scenery in the beam is drawn too).
        public List<Transform> PropsInBeam()
        {
            props.Clear();
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.InstanceID))
                if (!prop.TryGetComponent<global::ProjectorGame.Items.ItemMock>(out _) || prop.HasBeenInteractedWith.Value)
                    props.Add(prop.transform);
            foreach (var item in FindObjectsByType<global::ProjectorGame.Items.ItemMock>(FindObjectsSortMode.InstanceID))
                if (item.HasBeenInteractedWith && item.GetComponent<NetworkProp>() == null)
                    props.Add(item.transform);
            return props;
        }

        // The live drawing of the props in the beam (no colliders).
        public List<ProjectedItem> Capture() => Capture(PropsInBeam(), null, false);

        // Validate original geometry before projection clips collider outlines to the screen.
        public bool IsFullyInBeam(Transform prop)
        {
            var lens = transform.position;
            var hasVertices = false;
            prop.GetComponentsInChildren(filters);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || IsHighlight(filter.transform, prop) ||
                    !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                    continue;
                filter.sharedMesh.GetVertices(meshVertices);
                foreach (var vertex in meshVertices)
                {
                    hasVertices = true;
                    var point = filter.transform.TransformPoint(vertex);
                    if (lens.x - point.x <= MinDepth || point.x <= wallX)
                        return false;
                    var hit = lens + (point - lens) * ((wallX - lens.x) / (point.x - lens.x));
                    if (hit.z < screen.xMin || hit.z > screen.xMax ||
                        hit.y < screen.yMin || hit.y > screen.yMax)
                        return false;
                }
            }
            return hasVertices;
        }

        // Draws the given props, at the given root poses if any (the host's view of them when the level is built),
        // and with each part's collider outline if asked. The items returned are reused by the next capture.
        public List<ProjectedItem> Capture(IReadOnlyList<Transform> targets, IReadOnlyList<Pose> poses, bool withParts)
        {
            var items = new List<ProjectedItem>(targets.Count);
            var lens = transform.position;
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null)
                    continue;
                if (pool.Count <= items.Count)
                    pool.Add(new ProjectedItem());
                var item = pool[items.Count];
                if (Draw(item, targets[i], poses != null ? poses[i] : (Pose?)null, lens, withParts))
                    items.Add(item);
            }
            return items;
        }

        bool Draw(ProjectedItem item, Transform prop, Pose? pose, Vector3 lens, bool withParts)
        {
            item.Clear();
            item.Kind = KindOf(prop);
            // Moves the prop's parts from where they are here to the given pose.
            var toPose = pose.HasValue
                ? Matrix4x4.TRS(pose.Value.position, pose.Value.rotation, prop.lossyScale) * prop.worldToLocalMatrix
                : Matrix4x4.identity;

            prop.GetComponentsInChildren(filters);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || IsHighlight(filter.transform, prop))
                    continue;
                if (!filter.TryGetComponent(out MeshRenderer meshRenderer) || !meshRenderer.enabled)
                    continue;
                DrawPart(item, filter.sharedMesh, toPose * filter.transform.localToWorldMatrix, FinishOf(meshRenderer.sharedMaterial), lens, withParts);
            }
            return item.Vertices.Count > 0;
        }

        void DrawPart(ProjectedItem item, Mesh mesh, Matrix4x4 toWorld, Finish finish, Vector3 lens, bool withParts)
        {
            mesh.GetVertices(meshVertices);
            mesh.GetNormals(meshNormals);
            var hasNormals = meshNormals.Count == meshVertices.Count;
            var toWorldNormal = toWorld.inverse.transpose;
            EnsureCapacity(meshVertices.Count);

            hullPoints.Clear();
            for (var i = 0; i < meshVertices.Count; i++)
            {
                var point = toWorld.MultiplyPoint3x4(meshVertices[i]);
                world[i] = point;
                // Distance along the beam, which runs toward -x.
                var depth = lens.x - point.x;
                valid[i] = depth > MinDepth && point.x > wallX;
                if (!valid[i])
                    continue;
                var hit = lens + (point - lens) * ((wallX - lens.x) / (point.x - lens.x));
                var onLevel = ToLevel(new Vector2(hit.z, hit.y));
                projected[i] = new Vector3(onLevel.x, onLevel.y, StageDepth(depth));
                hullPoints.Add(onLevel);
                if (hasNormals)
                    lit[i] = Light(finish, toWorldNormal.MultiplyVector(meshNormals[i]).normalized, point, lens);
            }
            if (withParts)
                AddPart(item);

            var flipped = toWorld.determinant < 0f;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                mesh.GetTriangles(meshTriangles, submesh);
                for (var t = 0; t < meshTriangles.Count; t += 3)
                {
                    int a = meshTriangles[t], b = meshTriangles[t + 1], c = meshTriangles[t + 2];
                    if (!valid[a] || !valid[b] || !valid[c])
                        continue;
                    var normal = Vector3.Cross(world[b] - world[a], world[c] - world[a]);
                    if (flipped)
                        normal = -normal;
                    // Only the faces turned toward the lens.
                    if (normal.sqrMagnitude < 1e-20f || Vector3.Dot(normal, lens - world[a]) <= 0f)
                        continue;

                    var start = item.Vertices.Count;
                    if (hasNormals)
                    {
                        AddVertex(item, projected[a], lit[a]);
                        AddVertex(item, projected[b], lit[b]);
                        AddVertex(item, projected[c], lit[c]);
                    }
                    else
                    {
                        var face = normal.normalized;
                        AddVertex(item, projected[a], Light(finish, face, world[a], lens));
                        AddVertex(item, projected[b], Light(finish, face, world[b], lens));
                        AddVertex(item, projected[c], Light(finish, face, world[c], lens));
                    }
                    item.Triangles.Add(start);
                    item.Triangles.Add(start + 1);
                    item.Triangles.Add(start + 2);
                }
            }
        }

        // The convex outline of one part's projection, kept inside the level.
        void AddPart(ProjectedItem item)
        {
            if (hullPoints.Count < 3)
                return;
            var hull = ProjectedLevel.ConvexHull(hullPoints);
            hullPoints.Clear();
            foreach (var point in hull)
                hullPoints.Add(Vector2.Max(levelMin, Vector2.Min(levelMax, point)));
            hull = ProjectedLevel.ConvexHull(hullPoints);
            if (hull.Length >= 3 && ProjectedLevel.Area(hull) >= minimumPartArea)
                item.Parts.Add(hull);
        }

        // How a point on a part looks from the lens: soft light from the room (brighter on faces turned up), the
        // main light, a little light from the projector, and a highlight on smooth or metal parts.
        Color Light(Finish finish, Vector3 normal, Vector3 point, Vector3 lens)
        {
            var toLight = lens + lightOffset - point;
            var light = toLight.normalized;
            var falloff = 1f / (1f + lightFalloff * toLight.sqrMagnitude);
            var view = (lens - point).normalized;
            var ambient = Color.Lerp(groundLight, skyLight, normal.y * 0.5f + 0.5f);
            var diffuse = lightStrength * falloff * Mathf.Max(0f, Vector3.Dot(normal, light)) + lensLight * Mathf.Max(0f, Vector3.Dot(normal, view));
            // Metal takes on less of the soft light and more of its own color in the highlight.
            var color = finish.Color * (ambient * (1f - 0.4f * finish.Metallic) + Color.white * diffuse);

            var halfway = (light + view).normalized;
            var shine = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(normal, halfway)), Mathf.Lerp(4f, 64f, finish.Smoothness));
            color += Color.Lerp(Color.white, finish.Color, finish.Metallic) * (shine * finish.Smoothness * highlights * falloff);
            return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
        }

        static void AddVertex(ProjectedItem item, Vector3 position, Color color)
        {
            item.Vertices.Add(position);
            // Vertex colors reach the shader as they are, so they're stored in the project's (linear) color space.
            item.Colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
            item.Min = Vector2.Min(item.Min, position);
            item.Max = Vector2.Max(item.Max, position);
        }

        // Stage z for a distance along the beam. 1 / distance varies linearly across a flat face once it's
        // projected, so depth testing on the stage sorts faces exactly as the lens sees them.
        static float StageDepth(float depth) => StageNear + (StageFar - StageNear) * (1f - MinDepth / depth);

        void EnsureCapacity(int vertexCount)
        {
            if (world.Length >= vertexCount)
                return;
            world = new Vector3[vertexCount];
            projected = new Vector3[vertexCount];
            lit = new Color[vertexCount];
            valid = new bool[vertexCount];
        }

        // GrabHighlight's halo copies of the meshes aren't part of the prop.
        static bool IsHighlight(Transform part, Transform prop)
        {
            for (var t = part; t != null && t != prop; t = t.parent)
                if (t.name.EndsWith("(Grab Highlight)"))
                    return true;
            return false;
        }

        // Item mocks behave by category in the race; any other prop is plain ground.
        static ShapeKind KindOf(Transform prop)
        {
            if (!prop.TryGetComponent(out global::ProjectorGame.Items.ItemMock item))
                return ShapeKind.Platform;
            switch (item.category)
            {
                case global::ProjectorGame.Items.ItemCategory.Movement: return ShapeKind.Bounce;
                case global::ProjectorGame.Items.ItemCategory.Hazard: return ShapeKind.Hazard;
                case global::ProjectorGame.Items.ItemCategory.Bonus: return ShapeKind.Coin;
                default: return ShapeKind.Platform;
            }
        }

        static Finish FinishOf(Material material)
        {
            if (material == null)
                return new Finish { Color = Color.white };
            var color = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                : material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            return new Finish
            {
                Color = color,
                Smoothness = material.HasProperty(SmoothnessId) ? material.GetFloat(SmoothnessId) : 0f,
                Metallic = material.HasProperty(MetallicId) ? material.GetFloat(MetallicId) : 0f,
            };
        }

        Vector2 ToLevel(Vector2 onWall)
        {
            var u = (onWall.x - screen.xMin) / screen.width;
            var v = (onWall.y - screen.yMin) / screen.height;
            return new Vector2(Mathf.LerpUnclamped(levelMin.x, levelMax.x, u), Mathf.LerpUnclamped(levelMin.y, levelMax.y, v));
        }
    }
}
