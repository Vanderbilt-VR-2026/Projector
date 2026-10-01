using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Sits at the projector lens. Projects every prop's mesh from the lens onto the shadow wall (a plane of
    // constant x), takes each prop's convex outline, and maps the "screen" rectangle on the wall onto the
    // 2D level. This matches the real-time shadows the projector's spot light casts on that wall.
    public class ShadowCaster : MonoBehaviour
    {
        [Tooltip("x of the shadow wall's inner face.")]
        [SerializeField] float wallX = -3.92f;
        [Tooltip("Screen rectangle on the wall: x = z range, y = height range. Maps onto levelMin..levelMax.")]
        [SerializeField] Rect screen = new Rect(-2.6f, 0.3f, 6.7f, 3.35f);
        [SerializeField] Vector2 levelMin = new Vector2(-7f, 0f);
        [SerializeField] Vector2 levelMax = new Vector2(7f, 7f);
        [Tooltip("Outlines smaller than this (level units squared) are dropped as noise.")]
        [SerializeField] float minimumArea = 0.05f;

        public Rect Screen => screen;
        public float WallX => wallX;

        public List<Vector2[]> Capture()
        {
            var outlines = new List<Vector2[]>();
            var light = transform.position;
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.InstanceID))
            {
                var points = new List<Vector2>();
                foreach (var filter in prop.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;

                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var world = filter.transform.TransformPoint(vertex);
                        // Only geometry between the lens and the wall casts a shadow on it.
                        if (world.x >= light.x || world.x <= wallX)
                            continue;

                        var t = (wallX - light.x) / (world.x - light.x);
                        var hit = light + (world - light) * t;
                        points.Add(ToLevel(new Vector2(hit.z, hit.y)));
                    }
                }

                if (points.Count < 3)
                    continue;

                // Clamp to the level after projecting, then re-hull so the outline stays convex.
                var hull = ShadowLevel.ConvexHull(points);
                var clamped = new List<Vector2>(hull.Length);
                foreach (var point in hull)
                    clamped.Add(Vector2.Max(levelMin, Vector2.Min(levelMax, point)));
                hull = ShadowLevel.ConvexHull(clamped);

                if (hull.Length >= 3 && ShadowLevel.Area(hull) >= minimumArea)
                    outlines.Add(hull);
            }
            return outlines;
        }

        Vector2 ToLevel(Vector2 onWall)
        {
            var u = (onWall.x - screen.xMin) / screen.width;
            var v = (onWall.y - screen.yMin) / screen.height;
            return new Vector2(Mathf.LerpUnclamped(levelMin.x, levelMax.x, u), Mathf.LerpUnclamped(levelMin.y, levelMax.y, v));
        }
    }
}
