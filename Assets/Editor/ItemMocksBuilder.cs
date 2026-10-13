using System.Collections.Generic;
using System.IO;
using ProjectorGame.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Projector > Item Mocks > Build Item Mock Prefabs
/// Generates graybox mock-ups of the placeable game items (platforms, movers, hazards, bonus)
/// as grabbable prefabs in Assets/Prefabs/ItemMocks. Regenerate any time from the menu.
/// </summary>
public static class ItemMocksBuilder
{
    const string GeneratedRoot = "Assets/Generated/ItemMocks";
    const string ItemRoot = "Assets/Prefabs/ItemMocks";

    [MenuItem("Projector/Item Mocks/Build Item Mock Prefabs")]
    public static void BuildMenu()
    {
        BuildItemMocks();
        Debug.Log("[ItemMocks] Prefabs written to " + ItemRoot);
    }

    static void EnsureFolders()
    {
        Directory.CreateDirectory(GeneratedRoot);
        Directory.CreateDirectory(GeneratedRoot + "/Meshes");
        Directory.CreateDirectory(ItemRoot);
        AssetDatabase.Refresh();
    }

    // ================================================================= item mocks

    struct ItemSpec
    {
        public string name;
        public ItemCategory category;
        public string note;
    }

    static readonly ItemSpec[] k_Items =
    {
        new ItemSpec { name = "Plank", category = ItemCategory.Platform, note = "Long flat platform. Basic bridge piece." },
        new ItemSpec { name = "Block", category = ItemCategory.Platform, note = "1x1 solid block. Stack it to build walls or steps." },
        new ItemSpec { name = "L-Block", category = ItemCategory.Platform, note = "L-shaped platform with a ledge to hang off." },
        new ItemSpec { name = "Stairs", category = ItemCategory.Platform, note = "Three steps going up to the right." },
        new ItemSpec { name = "Ramp", category = ItemCategory.Platform, note = "Slope. Its shadow becomes a walkable incline." },
        new ItemSpec { name = "Arch", category = ItemCategory.Platform, note = "Bridge on two legs. Players can run under or over it." },
        new ItemSpec { name = "Trampoline", category = ItemCategory.Movement, note = "Bounces players high into the air." },
        new ItemSpec { name = "Spike Strip", category = ItemCategory.Hazard, note = "Touching the spikes sends you back to the start." },
        new ItemSpec { name = "Buzzsaw", category = ItemCategory.Hazard, note = "Spinning blade hazard." },
        new ItemSpec { name = "Coin", category = ItemCategory.Bonus, note = "Collect for bonus points." },
    };

    static Dictionary<string, GameObject> BuildItemMocks()
    {
        EnsureFolders();
        var light = Mat("Graybox_Light", new Color(0.78f, 0.78f, 0.8f), 0.3f);
        var mid = Mat("Graybox_Mid", new Color(0.55f, 0.56f, 0.6f), 0.3f);
        var dark = Mat("Graybox_Dark", new Color(0.28f, 0.29f, 0.32f), 0.4f);
        var yellow = Mat("Accent_Yellow", new Color(1f, 0.8f, 0.15f), 0.4f, 0f, null, null, new Color(0.25f, 0.18f, 0f));
        var red = Mat("Accent_Red", new Color(0.9f, 0.18f, 0.18f), 0.5f, 0f, null, null, new Color(0.2f, 0f, 0f));
        var gold = Mat("Accent_Gold", new Color(1f, 0.78f, 0.2f), 0.8f, 0.8f, null, null, new Color(0.25f, 0.17f, 0f));
        var steel = Mat("Accent_Steel", new Color(0.75f, 0.78f, 0.82f), 0.85f, 0.9f);

        var wedge = SaveMesh(BuildWedgeMesh(), "Wedge");
        var pyramid = SaveMesh(BuildPyramidMesh(), "Pyramid");

        var result = new Dictionary<string, GameObject>();
        foreach (var spec in k_Items)
        {
            var root = new GameObject(spec.name);
            var parts = root.transform;
            switch (spec.name)
            {
                case "Plank":
                    Part(PrimitiveType.Cube, "Board", parts, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.04f, 0.09f), mid);
                    break;
                case "Block":
                    Part(PrimitiveType.Cube, "Cube", parts, new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.12f, 0.12f), light);
                    break;
                case "L-Block":
                    Part(PrimitiveType.Cube, "Base", parts, new Vector3(0f, 0.04f, 0f), new Vector3(0.24f, 0.08f, 0.08f), light);
                    Part(PrimitiveType.Cube, "Upright", parts, new Vector3(-0.08f, 0.16f, 0f), new Vector3(0.08f, 0.16f, 0.08f), light);
                    break;
                case "Stairs":
                    for (int i = 0; i < 3; i++)
                        Part(PrimitiveType.Cube, "Step " + (i + 1), parts, new Vector3(-0.08f + i * 0.08f, 0.04f * (i + 1), 0f), new Vector3(0.08f, 0.08f * (i + 1), 0.09f), i % 2 == 0 ? light : mid);
                    break;
                case "Ramp":
                {
                    var r = MeshPart("Wedge", parts, new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.12f, 0.09f), wedge, mid);
                    r.AddComponent<MeshCollider>().convex = true;
                    break;
                }
                case "Arch":
                    Part(PrimitiveType.Cube, "Top", parts, new Vector3(0f, 0.145f, 0f), new Vector3(0.3f, 0.05f, 0.08f), light);
                    Part(PrimitiveType.Cube, "Leg L", parts, new Vector3(-0.12f, 0.06f, 0f), new Vector3(0.05f, 0.12f, 0.08f), mid);
                    Part(PrimitiveType.Cube, "Leg R", parts, new Vector3(0.12f, 0.06f, 0f), new Vector3(0.05f, 0.12f, 0.08f), mid);
                    break;
                case "Trampoline":
                    Disc("Frame", parts, new Vector3(0f, 0.03f, 0f), 0.16f, 0.02f, dark);
                    Disc("Pad", parts, new Vector3(0f, 0.045f, 0f), 0.13f, 0.012f, yellow);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                        Part(PrimitiveType.Cube, "Leg " + (i + 1), parts, new Vector3(Mathf.Cos(a) * 0.06f, 0.012f, Mathf.Sin(a) * 0.06f), new Vector3(0.015f, 0.024f, 0.015f), dark);
                    }
                    break;
                case "Spike Strip":
                    Part(PrimitiveType.Cube, "Base", parts, new Vector3(0f, 0.015f, 0f), new Vector3(0.24f, 0.03f, 0.08f), dark);
                    for (int i = 0; i < 4; i++)
                    {
                        var s = MeshPart("Spike " + (i + 1), parts, new Vector3(-0.09f + i * 0.06f, 0.03f, 0f), new Vector3(0.05f, 0.07f, 0.05f), pyramid, red);
                        s.AddComponent<MeshCollider>().convex = true;
                    }
                    break;
                case "Buzzsaw":
                {
                    var blade = Disc("Blade", parts, new Vector3(0f, 0.09f, 0f), 0.16f, 0.01f, red);
                    blade.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var tooth = Part(PrimitiveType.Cube, "Tooth " + (i + 1), parts, new Vector3(Mathf.Cos(a) * 0.085f, 0.09f + Mathf.Sin(a) * 0.085f, 0f), new Vector3(0.025f, 0.025f, 0.012f), steel);
                        tooth.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 45f);
                    }
                    var hub = Disc("Hub", parts, new Vector3(0f, 0.09f, 0f), 0.04f, 0.02f, steel);
                    hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(PrimitiveType.Cube, "Stand", parts, new Vector3(0f, 0.005f, 0f), new Vector3(0.1f, 0.01f, 0.05f), dark);
                    break;
                }
                case "Coin":
                {
                    var c = Disc("Coin", parts, new Vector3(0f, 0.04f, 0f), 0.08f, 0.012f, gold);
                    c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var star = Part(PrimitiveType.Cube, "Emblem", parts, new Vector3(0f, 0.04f, -0.007f), new Vector3(0.03f, 0.03f, 0.004f), yellow);
                    star.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                }
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = spec.category == ItemCategory.Bonus ? 0.1f : 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.throwOnDetach = true;

            var item = root.AddComponent<ItemMock>();
            item.displayName = spec.name;
            item.category = spec.category;
            item.gameplayNote = spec.note;

            string path = $"{ItemRoot}/{spec.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            result[spec.name] = prefab;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    static Dictionary<string, GameObject> LoadItemPrefabs()
    {
        var result = new Dictionary<string, GameObject>();
        foreach (var spec in k_Items)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemRoot}/{spec.name}.prefab");
            if (p != null)
                result[spec.name] = p;
        }
        return result;
    }


    // ================================================================= primitive helpers

    static GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 size, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        if (collider)
            g.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
        return g;
    }

    /// <summary>Item part: casts shadows (that's the whole point of the items).</summary>
    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    /// <summary>Flat disc of the given diameter/thickness with a box collider (cylinder capsules behave badly when flattened).</summary>
    static GameObject Disc(string name, Transform parent, Vector3 localPos, float diameter, float thickness, Material m)
    {
        return Cyl(name, parent, localPos, new Vector3(diameter, thickness / 2f, diameter), m, true);
    }

    static GameObject MeshPart(string name, Transform parent, Vector3 localPos, Vector3 size, Mesh mesh, Material m)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }


    // ================================================================= assets

    static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Texture tex = null, Vector2? tiling = null, Color? emission = null)
    {
        string path = $"{GeneratedRoot}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (tex != null)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m);
        return m;
    }



    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{GeneratedRoot}/Meshes/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            AssetDatabase.SaveAssets();
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>Unit wedge: 1x1 footprint centred on X/Z, rising from height 0 at -X to height 1 at +X.</summary>
    static Mesh BuildWedgeMesh()
    {
        var a = new Vector3(-0.5f, 0f, -0.5f);
        var b = new Vector3(0.5f, 0f, -0.5f);
        var c = new Vector3(0.5f, 1f, -0.5f);
        var d = new Vector3(-0.5f, 0f, 0.5f);
        var e = new Vector3(0.5f, 0f, 0.5f);
        var f = new Vector3(0.5f, 1f, 0.5f);
        var tris = new List<Vector3[]>
        {
            new[] { a, c, b },          // front
            new[] { d, e, f },          // back
            new[] { a, d, f }, new[] { a, f, c },   // slope
            new[] { b, c, f }, new[] { b, f, e },   // tall side
            new[] { a, b, e }, new[] { a, e, d },   // bottom
        };
        return FlatMesh("Wedge", tris);
    }

    /// <summary>Unit square pyramid: 1x1 base at y=0, apex at y=1.</summary>
    static Mesh BuildPyramidMesh()
    {
        var a = new Vector3(-0.5f, 0f, -0.5f);
        var b = new Vector3(0.5f, 0f, -0.5f);
        var c = new Vector3(0.5f, 0f, 0.5f);
        var d = new Vector3(-0.5f, 0f, 0.5f);
        var top = new Vector3(0f, 1f, 0f);
        var tris = new List<Vector3[]>
        {
            new[] { a, top, b }, new[] { b, top, c }, new[] { c, top, d }, new[] { d, top, a },
            new[] { a, b, c }, new[] { a, c, d },
        };
        return FlatMesh("Pyramid", tris);
    }

    static Mesh FlatMesh(string name, List<Vector3[]> tris)
    {
        var verts = new List<Vector3>();
        var idx = new List<int>();
        foreach (var t in tris)
        {
            // Make sure each triangle faces outward (away from the shape's centre).
            var center = new Vector3(0f, 0.4f, 0f);
            var n = Vector3.Cross(t[1] - t[0], t[2] - t[0]);
            var faceCenter = (t[0] + t[1] + t[2]) / 3f;
            bool flip = Vector3.Dot(n, faceCenter - center) < 0f;
            int start = verts.Count;
            verts.Add(t[0]);
            verts.Add(flip ? t[2] : t[1]);
            verts.Add(flip ? t[1] : t[2]);
            idx.Add(start); idx.Add(start + 1); idx.Add(start + 2);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
