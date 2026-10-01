using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // The 2D level captured from the projector room's shadows: one convex outline per platform, in
    // Platformer2D level coordinates (x right, y up). Set on every peer before the race scene loads.
    public static class ShadowLevel
    {
        static readonly List<Vector2[]> platforms = new List<Vector2[]>();

        public static IReadOnlyList<Vector2[]> Platforms => platforms;

        public static void Set(IEnumerable<Vector2[]> outlines)
        {
            platforms.Clear();
            platforms.AddRange(outlines);
        }

        // RPCs can't carry jagged arrays, so outlines travel as one flat point list plus per-outline counts.
        public static void Pack(IReadOnlyList<Vector2[]> outlines, out Vector2[] points, out int[] counts)
        {
            var flat = new List<Vector2>();
            counts = new int[outlines.Count];
            for (var i = 0; i < outlines.Count; i++)
            {
                counts[i] = outlines[i].Length;
                flat.AddRange(outlines[i]);
            }
            points = flat.ToArray();
        }

        public static List<Vector2[]> Unpack(Vector2[] points, int[] counts)
        {
            var outlines = new List<Vector2[]>(counts.Length);
            var offset = 0;
            foreach (var count in counts)
            {
                var outline = new Vector2[count];
                System.Array.Copy(points, offset, outline, 0, count);
                outlines.Add(outline);
                offset += count;
            }
            return outlines;
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
