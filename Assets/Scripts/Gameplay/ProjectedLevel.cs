using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // What a projected shape does in the 2D race. Comes from the prop's ItemMock category.
    public enum ShapeKind
    {
        Platform,   // solid ground (blocks, planks, ramps, stairs, arches, untyped props)
        Bounce,     // solid, and launches a runner that lands on top (trampoline)
        Hazard,     // solid, and touching it sends the runner back to the start (spikes, buzzsaw)
        Coin,       // not solid: becomes a coin pickup at the shape's center
    }

    // One prop as the projector draws it: its convex outline in level coordinates (x right, y up), its color
    // and what it does in the race.
    public struct ProjectedShape
    {
        public Vector2[] Outline;
        public Color Color;
        public ShapeKind Kind;
    }

    // Geometry and wire format for projected shapes.
    public static class ProjectedLevel
    {
        // RPCs can't carry jagged arrays, so outlines travel as one flat point list plus per-outline counts.
        public static void Pack(IReadOnlyList<ProjectedShape> shapes, out Vector2[] points, out int[] counts, out Color[] colors, out int[] kinds)
        {
            var flat = new List<Vector2>();
            counts = new int[shapes.Count];
            colors = new Color[shapes.Count];
            kinds = new int[shapes.Count];
            for (var i = 0; i < shapes.Count; i++)
            {
                counts[i] = shapes[i].Outline.Length;
                colors[i] = shapes[i].Color;
                kinds[i] = (int)shapes[i].Kind;
                flat.AddRange(shapes[i].Outline);
            }
            points = flat.ToArray();
        }

        public static List<ProjectedShape> Unpack(Vector2[] points, int[] counts, Color[] colors, int[] kinds)
        {
            var shapes = new List<ProjectedShape>(counts.Length);
            var offset = 0;
            for (var i = 0; i < counts.Length; i++)
            {
                var outline = new Vector2[counts[i]];
                System.Array.Copy(points, offset, outline, 0, counts[i]);
                var kind = kinds != null && i < kinds.Length ? (ShapeKind)kinds[i] : ShapeKind.Platform;
                shapes.Add(new ProjectedShape { Outline = outline, Color = colors[i], Kind = kind });
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

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
}
