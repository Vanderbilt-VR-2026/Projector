using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // One prop as the projector draws it: its convex outline in level coordinates (x right, y up) and its color.
    public struct ProjectedShape
    {
        public Vector2[] Outline;
        public Color Color;
    }

    // Geometry and wire format for projected shapes.
    public static class ProjectedLevel
    {
        // RPCs can't carry jagged arrays, so outlines travel as one flat point list plus per-outline counts.
        public static void Pack(IReadOnlyList<ProjectedShape> shapes, out Vector2[] points, out int[] counts, out Color[] colors)
        {
            var flat = new List<Vector2>();
            counts = new int[shapes.Count];
            colors = new Color[shapes.Count];
            for (var i = 0; i < shapes.Count; i++)
            {
                counts[i] = shapes[i].Outline.Length;
                colors[i] = shapes[i].Color;
                flat.AddRange(shapes[i].Outline);
            }
            points = flat.ToArray();
        }

        public static List<ProjectedShape> Unpack(Vector2[] points, int[] counts, Color[] colors)
        {
            var shapes = new List<ProjectedShape>(counts.Length);
            var offset = 0;
            for (var i = 0; i < counts.Length; i++)
            {
                var outline = new Vector2[counts[i]];
                System.Array.Copy(points, offset, outline, 0, counts[i]);
                shapes.Add(new ProjectedShape { Outline = outline, Color = colors[i] });
                offset += counts[i];
            }
            return shapes;
        }

        // Andrew's monotone chain. Returns the hull counter-clockwise, without the repeated first point.
        public static Vector2[] ConvexHull(List<Vector2> points)
        {
            if (points.Count < 3)
                return points.ToArray();

            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new Vector2[points.Count * 2];
            var k = 0;
            for (var i = 0; i < points.Count; i++)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], points[i]) <= 0f)
                    k--;
                hull[k++] = points[i];
            }
            for (int i = points.Count - 2, lower = k + 1; i >= 0; i--)
            {
                while (k >= lower && Cross(hull[k - 2], hull[k - 1], points[i]) <= 0f)
                    k--;
                hull[k++] = points[i];
            }

            var result = new Vector2[k - 1];
            System.Array.Copy(hull, result, k - 1);
            return result;
        }

        public static float Area(Vector2[] outline)
        {
            var area = 0f;
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) / 2f;
        }

        // Grows a counter-clockwise convex outline by `distance` on every side (for the drawn border):
        // each edge moves out along its normal and neighbouring edges are re-intersected.
        public static Vector2[] Expand(Vector2[] outline, float distance)
        {
            var n = outline.Length;
            var result = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var previous = outline[(i - 1 + n) % n];
                var current = outline[i];
                var next = outline[(i + 1) % n];
                var normalIn = Outward(current - previous);
                var normalOut = Outward(next - current);
                var bisector = (normalIn + normalOut).normalized;
                var cos = Mathf.Max(0.25f, Vector2.Dot(bisector, normalOut));
                result[i] = current + bisector * (distance / cos);
            }
            return result;
        }

        static Vector2 Outward(Vector2 edge) => new Vector2(edge.y, -edge.x).normalized;

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
}
