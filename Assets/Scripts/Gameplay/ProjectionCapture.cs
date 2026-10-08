using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Sits at the projector lens. Projects every prop's mesh from the lens onto the projection wall (a plane of
    // constant x) and maps the screen rectangle on that wall onto the 2D level, so each prop becomes a 2D shape
    // drawn exactly where the prop sits in the beam.
    public class ProjectionCapture : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("x of the projection wall's inner face.")]
        [SerializeField] float wallX = -3.92f;
        [Tooltip("Screen rectangle on the wall: x = z range, y = height range. Maps onto levelMin..levelMax.")]
        [SerializeField] Rect screen = new Rect(-2.3f, 1.1f, 6f, 3f);
        [SerializeField] Vector2 levelMin = new Vector2(-10f, 0f);
        [SerializeField] Vector2 levelMax = new Vector2(10f, 10f);
        [Tooltip("Shapes smaller than this (level units squared) are dropped as noise.")]
        [SerializeField] float minimumArea = 0.05f;

        public Rect Screen => screen;
        public float WallX => wallX;
        public Vector2 LevelMin => levelMin;
        public Vector2 LevelMax => levelMax;

        readonly List<Vector2> points = new List<Vector2>();

        public List<ProjectedShape> Capture()
        {
            var shapes = new List<ProjectedShape>();
            var lens = transform.position;
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.InstanceID))
            {
                points.Clear();
                Color? color = null;
                foreach (var filter in prop.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;

                    if (color == null && filter.TryGetComponent(out MeshRenderer meshRenderer) && meshRenderer.sharedMaterial != null)
                        color = ColorOf(meshRenderer.sharedMaterial);

                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var world = filter.transform.TransformPoint(vertex);
                        // Only geometry between the lens and the wall is in the picture.
                        if (world.x >= lens.x || world.x <= wallX)
                            continue;

                        var t = (wallX - lens.x) / (world.x - lens.x);
                        var hit = lens + (world - lens) * t;
                        points.Add(ToLevel(new Vector2(hit.z, hit.y)));
                    }
                }

                if (points.Count < 3)
                    continue;

                // Clamp to the level after projecting, then re-hull so the outline stays convex.
                var hull = ProjectedLevel.ConvexHull(points);
                var clamped = new List<Vector2>(hull.Length);
                foreach (var point in hull)
                    clamped.Add(Vector2.Max(levelMin, Vector2.Min(levelMax, point)));
                hull = ProjectedLevel.ConvexHull(clamped);

                if (hull.Length >= 3 && ProjectedLevel.Area(hull) >= minimumArea)
                    shapes.Add(new ProjectedShape { Outline = hull, Color = color ?? Color.white });
            }
            return shapes;
        }

        static Color ColorOf(Material material)
        {
            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);
            return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
        }

        Vector2 ToLevel(Vector2 onWall)
        {
            var u = (onWall.x - screen.xMin) / screen.width;
            var v = (onWall.y - screen.yMin) / screen.height;
            return new Vector2(Mathf.LerpUnclamped(levelMin.x, levelMax.x, u), Mathf.LerpUnclamped(levelMin.y, levelMax.y, v));
        }
    }
}
