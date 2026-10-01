using System.Linq;
using UnityEditor;
using UnityEngine;
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

        // One color per player slot (SessionService.MaxPlayers), shared by lobby pads and 2D avatar dummies.
        public static readonly Color[] PlayerColors =
        {
            new Color(0.86f, 0.24f, 0.22f),
            new Color(0.22f, 0.45f, 0.88f),
            new Color(0.25f, 0.72f, 0.33f),
            new Color(0.95f, 0.78f, 0.2f)
        };

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
    }
}
