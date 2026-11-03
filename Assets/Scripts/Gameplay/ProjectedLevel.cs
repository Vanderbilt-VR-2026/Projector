using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // What a projected item does in the 2D race. Comes from the prop's ItemMock category.
    public enum ShapeKind
    {
        Platform,   // solid ground (blocks, planks, ramps, stairs, arches, untyped props)
        Bounce,     // solid, and launches a runner that lands on top (trampoline)
        Hazard,     // solid, and touching it sends the runner back to the start (spikes, buzzsaw)
        Coin,       // not solid: becomes a coin pickup at the drawing's center
    }

    // One prop as the projector draws it, in level coordinates (x right, y up): a mesh of its visible faces,
    // shaded in the prop's own colors, with its corners and edges inked in (z is depth on the stage, so nearer
    // faces cover farther ones); the convex outline of each of its parts, which become its colliders; and what
    // it does in the race.
    public class ProjectedItem
    {
        public ShapeKind Kind;
        public readonly List<Vector2[]> Parts = new List<Vector2[]>();
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();
        public Vector2 Min;
        public Vector2 Max;

        public Vector2 Center => (Min + Max) / 2f;

        public void Clear()
        {
            Parts.Clear();
            Vertices.Clear();
            Colors.Clear();
            Triangles.Clear();
            Min = new Vector2(float.MaxValue, float.MaxValue);
            Max = new Vector2(float.MinValue, float.MinValue);
        }

        public void CopyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = Vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    // Geometry for projected items.
    public static class ProjectedLevel
    {
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
