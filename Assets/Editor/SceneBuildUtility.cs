using System.Collections.Generic;
using System.Linq;
using Projector.Networking;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Projector.Editor
{
    // Shared building blocks for the Projector/* scene builders: geometry, materials, the XR rig, and world-space UI.
    public static class SceneBuildUtility
    {
        public const string XrRigPath = "Assets/Samples/XR Interaction Toolkit/3.3.0/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        public static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.09f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.1f, 0.45f, 0.62f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.8f, 0.3f);
        public static readonly Color BackgroundColor = new Color(0.06f, 0.065f, 0.075f);


        // ---------- Geometry ----------

        public static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, localScale, Vector3.zero, material, collider);
        }

        public static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material, bool collider)
        {
            return CreatePrimitive(PrimitiveType.Cylinder, name, parent, localPosition, localScale, localEuler, material, collider);
        }

        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // A closed box room centered on `center` (floor at center.y), with walls on all four sides.
        public static GameObject CreateRoomShell(string name, Vector3 center, Vector3 size, Material walls, Material floor)
        {
            const float thickness = 0.16f;
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            root.transform.position = center;

            CreateCube("Floor", root.transform, new Vector3(0f, -thickness / 2f, 0f), new Vector3(size.x, thickness, size.z), floor, true);
            CreateCube("Ceiling", root.transform, new Vector3(0f, size.y, 0f), new Vector3(size.x, thickness, size.z), walls, false);
            CreateCube("Front Wall", root.transform, new Vector3(0f, size.y / 2f, size.z / 2f), new Vector3(size.x, size.y, thickness), walls, true);
            CreateCube("Back Wall", root.transform, new Vector3(0f, size.y / 2f, -size.z / 2f), new Vector3(size.x, size.y, thickness), walls, true);
            CreateCube("Left Wall", root.transform, new Vector3(-size.x / 2f, size.y / 2f, 0f), new Vector3(thickness, size.y, size.z), walls, true);
            CreateCube("Right Wall", root.transform, new Vector3(size.x / 2f, size.y / 2f, 0f), new Vector3(thickness, size.y, size.z), walls, true);
            return root;
        }

        // Shadows default off: a directional light's shadows would black out a room with a ceiling.
        public static Light CreateDirectionalLight(string name, Color color, float intensity, Vector3 euler, LightShadows shadows = LightShadows.None)
        {
            GameObject lightObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create " + name);
            lightObject.transform.rotation = Quaternion.Euler(euler);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            return light;
        }

        public static void SetFlatAmbient(Color color)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = color;
            RenderSettings.fog = false;
        }

        // A faint pyramid of light from `lens` to the four `corners` of a projected image, drawn with the
        // Projector Beam shader. The mesh is saved at `meshPath` (updated in place on a rebuild).
        public static GameObject CreateBeam(string name, Transform parent, Vector3 lens, Vector3[] corners, string meshPath)
        {
            const string materialPath = "Assets/Generated/Projection/ProjectorBeam.mat";
            EnsureFolder("Assets/Generated/Projection");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Projector/Projector Beam"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var vertices = new Vector3[8];
            var colors = new Color[8];
            var triangles = new int[12];
            for (int i = 0; i < 4; i++)
            {
                vertices[i * 2] = lens;
                vertices[i * 2 + 1] = corners[i];
                colors[i * 2] = new Color(1f, 1f, 1f, 0.045f);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0.006f);
            }
            // Each side: lens, this corner, next corner (the lens vertex is duplicated per side).
            for (int i = 0; i < 4; i++)
            {
                triangles[i * 3] = i * 2;
                triangles[i * 3 + 1] = i * 2 + 1;
                triangles[i * 3 + 2] = ((i + 1) % 4) * 2 + 1;
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var beam = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(beam, "Create " + name);
            beam.transform.SetParent(parent);
            beam.AddComponent<MeshFilter>().sharedMesh = mesh;
            var beamRenderer = beam.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = material;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            return beam;
        }

        // A unit box (-0.5..0.5) with rounded edges and corners of `radius`, `segments` quads across each face: a
        // low-poly building block for soft shapes (radius 0.5 gives a sphere). Saved at `meshPath`, updated in place.
        public static Mesh CreateRoundedBoxMesh(string meshPath, int segments, float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float inner = 0.5f - radius;
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in faces)
            {
                // Two axes spanning the face, chosen so every face winds outward.
                var u = new Vector3(normal.y, normal.z, normal.x);
                var v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                for (int j = 0; j <= segments; j++)
                {
                    for (int i = 0; i <= segments; i++)
                    {
                        var onCube = normal * 0.5f + u * (i / (float)segments - 0.5f) + v * (j / (float)segments - 0.5f);
                        var core = Vector3.Max(-Vector3.one * inner, Vector3.Min(Vector3.one * inner, onCube));
                        var outward = onCube - core;
                        var direction = outward.sqrMagnitude > 1e-8f ? outward.normalized : normal;
                        vertices.Add(core + direction * radius);
                        normals.Add(direction);
                    }
                }
                for (int j = 0; j < segments; j++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        int a = start + j * (segments + 1) + i;
                        triangles.AddRange(new[] { a, a + 1, a + segments + 1, a + 1, a + segments + 2, a + segments + 1 });
                    }
                }
            }

            var mesh = LoadOrCreateMesh(meshPath, "Rounded Box");
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A width x height rectangle in the local XY plane whose border `fuzz` wide fades out (vertex alpha): a
        // soft glow or square of light when drawn additively with the Projector Beam shader.
        public static Mesh CreateSoftRectMesh(string meshPath, float width, float height, float fuzz)
        {
            float[] xs = { -width / 2f - fuzz, -width / 2f + fuzz, width / 2f - fuzz, width / 2f + fuzz };
            float[] ys = { -height / 2f - fuzz, -height / 2f + fuzz, height / 2f - fuzz, height / 2f + fuzz };
            var vertices = new Vector3[16];
            var colors = new Color[16];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    vertices[y * 4 + x] = new Vector3(xs[x], ys[y], 0f);
                    bool inner = x is 1 or 2 && y is 1 or 2;
                    colors[y * 4 + x] = new Color(1f, 1f, 1f, inner ? 1f : 0f);
                }
            }
            var triangles = new int[9 * 6];
            for (int y = 0, t = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++, t += 6)
                {
                    // Split each corner cell along the diagonal through its lit inner corner, so the fade has no crease.
                    int i = y * 4 + x;
                    bool alongMain = x == y;
                    triangles[t] = i;
                    triangles[t + 1] = i + 4;
                    triangles[t + 2] = alongMain ? i + 5 : i + 1;
                    triangles[t + 3] = alongMain ? i : i + 1;
                    triangles[t + 4] = i + 5;
                    triangles[t + 5] = alongMain ? i + 1 : i + 4;
                }
            }

            var mesh = LoadOrCreateMesh(meshPath, "Soft Rect");
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // An additive glow material on the Projector Beam shader (pair with CreateSoftRectMesh).
        public static Material CreateGlowMaterial(string folder, string name, Color tint, float intensity)
        {
            EnsureFolder(folder);
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Projector/Projector Beam"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Tint", tint);
            material.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Combines `parts` (each a mesh and its world matrix) into one mesh asset at `meshPath`: one draw call.
        public static Mesh CombineIntoMesh(string meshPath, List<CombineInstance> parts)
        {
            var mesh = LoadOrCreateMesh(meshPath, System.IO.Path.GetFileNameWithoutExtension(meshPath));
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A renderer for a generated mesh, without shadows.
        public static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return go;
        }

        public static Mesh LoadOrCreateMesh(string path, string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            return mesh;
        }

        // ---------- Assets ----------

        // Updates an existing material in place so its GUID (and every scene that references it) survives a rebuild.
        public static Material CreateMaterial(string folder, string name, Color color, float smoothness, float metallic)
        {
            EnsureFolder(folder);
            string path = folder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader) { name = name };
            else
                material.shader = shader;

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        // Flat color with no lighting, for the projected 2D look. Tint per renderer with _BaseColor.
        public static Material CreateUnlitMaterial(string folder, string name, Color color)
        {
            EnsureFolder(folder);
            string path = folder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader) { name = name };
            else
                material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        // The theater's wood (landing floor and counter, projector room stage): the plank texture, tinted and repeated
        // `tiling` times across each face. Lit, but with no normal or detail maps, to stay cheap on Quest.
        public static Material CreateWoodMaterial(string folder, string name, Vector2 tiling, Color tint, float smoothness = 0.25f)
        {
            Material material = CreateMaterial(folder, name, tint, smoothness, 0f);
            Texture2D planks = CreatePlankTexture();
            material.SetTexture("_BaseMap", planks);
            material.SetTexture("_MainTex", planks);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_MainTex", tiling);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Four vertical planks with grain and staggered end joints, 128 x 512 so a tile is 0.6 m wide by 2.4 m long.
        // None of the installed packages ship a wood texture, so it's generated once (an existing file is kept).
        public static Texture2D CreatePlankTexture()
        {
            const string path = "Assets/Generated/Theater/StageWood.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int width = 128, height = 512, planks = 4, plankWidth = width / planks;
            var random = new System.Random(7);
            var tone = new float[planks];
            var joint = new int[planks];
            for (int p = 0; p < planks; p++)
            {
                tone[p] = 0.85f + (float)random.NextDouble() * 0.3f;
                joint[p] = random.Next(height);
            }

            // Grain runs along the plank (y); blending with the sample one tile down makes it wrap seamlessly.
            float Grain(int x, float y) => 0.65f * Mathf.PerlinNoise(x * 0.35f, y * 0.012f) + 0.35f * Mathf.PerlinNoise(x * 0.06f + 40f, y * 0.003f);

            var light = new Color(0.56f, 0.36f, 0.2f);
            var dark = new Color(0.34f, 0.2f, 0.1f);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float blend = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    int p = x / plankWidth;
                    int u = x % plankWidth;
                    float grain = Mathf.Lerp(Grain(x + p * 17, y), Grain(x + p * 17, y - height), blend);
                    Color color = Color.Lerp(dark, light, grain) * tone[p];
                    int fromJoint = Mathf.Abs(y - joint[p]);
                    if (u == 0 || u == plankWidth - 1 || Mathf.Min(fromJoint, height - fromJoint) < 2)
                        color *= 0.45f;
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            EnsureFolder("Assets/Generated/Theater");
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string child = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }

        // Adds the scene to the build list (Netcode can only sync scenes that are in it), keeping existing order.
        public static void AddSceneToBuildSettings(string scenePath, bool first = false)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (first)
                scenes.Insert(0, entry);
            else
                scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void RemoveSceneFromBuildSettings(string scenePath)
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToArray();
        }

        // ---------- XR rig ----------

        public static GameObject CreateXrRig(string name, Vector3 position, Quaternion rotation, Color background)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPath);
            if (prefab == null)
                throw new MissingReferenceException("XR Origin prefab was not found at " + XrRigPath);

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(rig, "Create " + name);
            rig.name = name;
            rig.transform.SetPositionAndRotation(position, rotation);

            Camera camera = rig.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
            }

            return rig;
        }

        // ---------- World-space UI ----------

        // A world-space canvas laid out in pixels; scale 0.001 makes 1000 px = 1 m.
        public static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 size, Camera worldCamera)
        {
            var canvasObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create " + name);
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            canvas.GetComponent<RectTransform>().sizeDelta = size;
            canvasObject.transform.localScale = Vector3.one * 0.001f;
            return canvas;
        }

        // Places a canvas `distance` meters in front of the camera, facing it.
        public static void PlaceInFrontOf(Transform target, Camera camera, float distance)
        {
            if (camera == null)
                return;

            var position = camera.transform.position + camera.transform.forward * distance;
            target.SetPositionAndRotation(position, Quaternion.LookRotation(position - camera.transform.position, camera.transform.up));
        }

        public static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("XR Event System");
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create XR Event System");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<XRUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        public static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(imageObject, "Create Menu Image");
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        // A full-size background panel used as one page of a menu.
        public static RectTransform CreatePanel(string objectName, Transform parent)
        {
            var panel = CreateImage(objectName, parent, PanelColor);
            SetFullSize(panel.rectTransform);
            return panel.rectTransform;
        }

        public static Text CreateText(string value, Transform parent, int fontSize, Color color, string objectName = null)
        {
            var textObject = new GameObject(objectName ?? (string.IsNullOrEmpty(value) ? "Text" : value));
            Undo.RegisterCreatedObjectUndo(textObject, "Create Menu Text");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string label, Transform parent, int fontSize = 36)
        {
            var buttonObject = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(buttonObject, "Create Menu Button");
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = ButtonColor;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(label, buttonObject.transform, fontSize, Color.white);
            SetFullSize(text.rectTransform);
            return button;
        }

        public static void SetFullSize(RectTransform rectTransform)
        {
            SetAnchors(rectTransform, Vector2.zero, Vector2.one);
        }

        public static void SetAnchors(RectTransform rectTransform, Vector2 min, Vector2 max)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // ---------- Networking ----------

        // Netcode identifies NetworkObjects by a GlobalObjectIdHash it only computes when validating an object
        // that already has a file ID. Scripted builds never trigger that, so validate explicitly after saving.
        static readonly System.Reflection.MethodInfo ValidateNetworkObject =
            typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Saves a scene containing NetworkObjects: save (assigns file IDs), add to the build list, hash, save again.
        public static void SaveNetworkScene(Scene scene, string scenePath, bool firstInBuild = false)
        {
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath, firstInBuild);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                    ValidateNetworkObject.Invoke(networkObject, null);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        public static void SaveNetworkPrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            var asset = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
            ValidateNetworkObject.Invoke(asset, null);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        // Owner-authority transform sync: the owning player moves the object and everyone else follows.
        public static NetworkTransform AddOwnerNetworkTransform(GameObject target, bool syncRotation = true)
        {
            var networkTransform = target.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncRotAngleX = networkTransform.SyncRotAngleY = networkTransform.SyncRotAngleZ = syncRotation;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            return networkTransform;
        }

        // A scene object that spawns `prefab` for each player at one of `spawnPoints` (one per slot),
        // optionally placing that player's XR rig at the matching `rigPoints` entry.
        public static PlayerSpawner CreatePlayerSpawner(string prefabPath, Vector3[] spawnPositions, Vector3[] rigPositions = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
            if (prefab == null)
                throw new MissingReferenceException("Network prefab was not found at " + prefabPath + ". Run Projector > Build Network Prefabs.");

            var root = new GameObject("Player Spawner");
            Undo.RegisterCreatedObjectUndo(root, "Create Player Spawner");
            root.AddComponent<NetworkObject>();
            var spawner = root.AddComponent<PlayerSpawner>();
            SetReference(spawner, "prefab", prefab);
            SetReferences(spawner, "spawnPoints", CreatePoints("Spawn", root.transform, spawnPositions));
            if (rigPositions != null)
                SetReferences(spawner, "rigPoints", CreatePoints("Rig", root.transform, rigPositions));

            new GameObject("Offline Host").AddComponent<OfflineHost>();
            return spawner;
        }

        static Object[] CreatePoints(string prefix, Transform parent, Vector3[] positions)
        {
            var points = new Object[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"{prefix} {i + 1}").transform;
                point.SetParent(parent);
                point.position = positions[i];
                points[i] = point;
            }
            return points;
        }

        // Wires a private [SerializeField] on a runtime component, the way the builders hand scene objects to scripts.
        public static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException($"{target.GetType().Name} has no serialized field '{propertyName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetReferences(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new MissingReferenceException($"{target.GetType().Name} has no serialized array '{propertyName}'.");
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
