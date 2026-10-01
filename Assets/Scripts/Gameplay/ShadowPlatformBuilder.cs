using System.Collections.Generic;
using UnityEngine;

namespace Projector.Gameplay
{
    // Turns the captured shadow outlines into solid platforms on the 2D play plane (z = 0), with a coin
    // floating over each one. Every peer builds the same level from the same outlines. With no captured
    // level (scene opened directly) it builds a sample row of stepping stones.
    public class ShadowPlatformBuilder : MonoBehaviour
    {
        [SerializeField] Material platformMaterial;
        [SerializeField] Material coinMaterial;
        [SerializeField] float depth = 1.5f;

        void Start()
        {
            var outlines = ShadowLevel.Platforms.Count > 0 ? ShadowLevel.Platforms : SampleLevel();
            for (var i = 0; i < outlines.Count; i++)
                Build(i, outlines[i]);
        }

        static IReadOnlyList<Vector2[]> SampleLevel()
        {
            return new[]
            {
                Rectangle(-4.5f, 2.6f, 2.2f, 0.4f),
                Rectangle(0f, 3.4f, 2.2f, 0.4f),
                Rectangle(4.5f, 2.6f, 2.2f, 0.4f)
            };
        }

        static Vector2[] Rectangle(float centerX, float centerY, float width, float height)
        {
            float x0 = centerX - width / 2f, x1 = centerX + width / 2f, y0 = centerY - height / 2f, y1 = centerY + height / 2f;
            return new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
        }

        void Build(int index, Vector2[] outline)
        {
            var platform = new GameObject("Shadow Platform " + (index + 1));
            platform.transform.SetParent(transform, false);
            var mesh = Extrude(outline, depth);
            platform.AddComponent<MeshFilter>().sharedMesh = mesh;
            platform.AddComponent<MeshRenderer>().sharedMaterial = platformMaterial;
            var collider = platform.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;

            // Coin just above the platform's highest point, centered over it.
            var top = outline[0];
            var center = Vector2.zero;
            foreach (var point in outline)
            {
                center += point;
                if (point.y > top.y)
                    top = point;
            }
            center /= outline.Length;

            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Coin " + (index + 1);
            coin.transform.SetParent(transform, false);
            coin.transform.localPosition = new Vector3(center.x, top.y + 0.6f, 0f);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coin.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
            coin.GetComponent<MeshRenderer>().sharedMaterial = coinMaterial;
            Destroy(coin.GetComponent<Collider>());
            var trigger = coin.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            coin.AddComponent<Coin>();
        }

        // A prism from a counter-clockwise convex outline: front face toward the viewer (-z), back face, sides.
        static Mesh Extrude(Vector2[] outline, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var front = -depth / 2f;
            var back = depth / 2f;
            var n = outline.Length;

            var frontStart = vertices.Count;
            foreach (var p in outline)
                vertices.Add(new Vector3(p.x, p.y, front));
            for (var i = 1; i < n - 1; i++)
                triangles.AddRange(new[] { frontStart, frontStart + i + 1, frontStart + i });

            var backStart = vertices.Count;
            foreach (var p in outline)
                vertices.Add(new Vector3(p.x, p.y, back));
            for (var i = 1; i < n - 1; i++)
                triangles.AddRange(new[] { backStart, backStart + i, backStart + i + 1 });

            // Sides get their own vertices so each face has a flat normal.
            for (var i = 0; i < n; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % n];
                var s = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, front));
                vertices.Add(new Vector3(b.x, b.y, front));
                vertices.Add(new Vector3(b.x, b.y, back));
                vertices.Add(new Vector3(a.x, a.y, back));
                triangles.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            var mesh = new Mesh { name = "Shadow Platform" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
