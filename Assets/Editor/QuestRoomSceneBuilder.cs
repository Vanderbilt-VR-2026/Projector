using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Editor
{
    public static class QuestRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ProjectorRoom.unity";
        private const string GeneratedFolder = "Assets/Generated/QuestRoom";
        private const string FoodPackFolder = "Assets/ThirdParty/Quaternius/UltimateFoodPack";
        private const string PortraitPath = "Assets/Art/DiermeierPortrait.png";
        private const string XrRigPath = "Assets/Samples/XR Interaction Toolkit/3.3.0/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        [MenuItem("Projector/Build Quest Room Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(GeneratedFolder);
            ConfigurePortraitTexture();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Projector Room";

            Material whiteWall = CreateMaterial("WhiteWall", new Color(0.93f, 0.93f, 0.91f), 0.05f, 0f);
            Material sideWall = CreateMaterial("SideWall", new Color(0.74f, 0.75f, 0.74f), 0.02f, 0f);
            Material floor = CreateMaterial("Floor", new Color(0.20f, 0.18f, 0.16f), 0.18f, 0f);
            Material wood = CreateMaterial("WalnutTable", new Color(0.24f, 0.095f, 0.035f), 0.30f, 0f);
            Material metal = CreateMaterial("DarkMetal", new Color(0.055f, 0.06f, 0.07f), 0.55f, 0.75f);
            Material projectorBody = CreateMaterial("ProjectorBody", new Color(0.86f, 0.87f, 0.88f), 0.42f, 0.05f);
            Material lens = CreateMaterial("ProjectorLens", new Color(0.045f, 0.075f, 0.105f), 0.75f, 0.35f);
            Material frame = CreateMaterial("PortraitFrame", new Color(0.24f, 0.15f, 0.055f), 0.35f, 0.1f);
            Material portrait = CreatePortraitMaterial();

            CreateRoom(whiteWall, sideWall, floor);
            CreateTable(wood, metal);
            CreateFruitObjects();
            CreateProjector(projectorBody, metal, lens);
            CreatePortrait(portrait, frame);
            CreateLighting();
            CreateXrRig();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.65f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.33f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.12f, 0.11f);
            RenderSettings.ambientIntensity = 0.72f;
            RenderSettings.fog = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            CapturePreview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Quest room scene created successfully at " + ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildScene();
        }

        [MenuItem("Projector/Capture Room Preview")]
        public static void CapturePreview()
        {
            GameObject cameraObject = new GameObject("Temporary Preview Camera");
            Camera previewCamera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.62f, -3.25f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.18f, 0.72f));
            previewCamera.fieldOfView = 61f;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 30f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.06f, 0.065f, 0.075f);
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;

            RenderTexture renderTexture = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            previewCamera.targetTexture = renderTexture;
            previewCamera.Render();
            RenderTexture.active = renderTexture;

            Texture2D screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes("/tmp/ProjectorRoomPreview.png", screenshot.EncodeToPNG());

            previewCamera.targetTexture = null;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(cameraObject);
            Debug.Log("Room preview captured at /tmp/ProjectorRoomPreview.png");
        }

        private static void CreateRoom(Material whiteWall, Material sideWall, Material floor)
        {
            GameObject root = new GameObject("Room");
            CreateCube("Floor", root.transform, new Vector3(0f, -0.08f, 0f), new Vector3(8f, 0.16f, 8f), floor, true);
            CreateCube("White Feature Wall", root.transform, new Vector3(0f, 2.25f, 4f), new Vector3(8f, 4.5f, 0.16f), whiteWall, true);
            CreateCube("Left Wall", root.transform, new Vector3(-4f, 2.25f, 0f), new Vector3(0.16f, 4.5f, 8f), sideWall, true);
            CreateCube("Right Wall", root.transform, new Vector3(4f, 2.25f, 0f), new Vector3(0.16f, 4.5f, 8f), sideWall, true);
            CreateCube("Ceiling", root.transform, new Vector3(0f, 4.5f, 0f), new Vector3(8f, 0.12f, 8f), whiteWall, false);
        }

        private static void CreateTable(Material wood, Material metal)
        {
            GameObject root = new GameObject("Long Table");
            root.transform.position = new Vector3(0f, 0f, 0.55f);

            CreateCube("Tabletop", root.transform, new Vector3(0f, 0.79f, 0f), new Vector3(5.25f, 0.14f, 1.35f), wood, true);
            CreateCube("Front Apron", root.transform, new Vector3(0f, 0.66f, -0.55f), new Vector3(4.85f, 0.18f, 0.08f), wood, true);
            CreateCube("Back Apron", root.transform, new Vector3(0f, 0.66f, 0.55f), new Vector3(4.85f, 0.18f, 0.08f), wood, true);

            Vector3[] legPositions =
            {
                new Vector3(-2.30f, 0.36f, -0.48f), new Vector3(-2.30f, 0.36f, 0.48f),
                new Vector3(2.30f, 0.36f, -0.48f), new Vector3(2.30f, 0.36f, 0.48f)
            };
            for (int i = 0; i < legPositions.Length; i++)
                CreateCube("Leg " + (i + 1), root.transform, legPositions[i], new Vector3(0.13f, 0.72f, 0.13f), metal, true);
        }

        private static void CreateFruitObjects()
        {
            GameObject root = new GameObject("Five Fruit Objects");
            string[] names = { "Green Apple", "Avocado", "Banana", "Pumpkin", "Tomato" };
            string[] assetPaths =
            {
                FoodPackFolder + "/Apple Green/Apple_Green.fbx",
                FoodPackFolder + "/Avocado/Avocado.fbx",
                FoodPackFolder + "/Banana/Banana.fbx",
                FoodPackFolder + "/Pumpkin/Pumpkin.fbx",
                FoodPackFolder + "/Tomato/Tomato.fbx"
            };
            Vector3[] positions =
            {
                new Vector3(-0.72f, 0f, 0.10f),
                new Vector3(-0.36f, 0f, 0.20f),
                new Vector3(0.00f, 0f, 0.08f),
                new Vector3(0.38f, 0f, 0.20f),
                new Vector3(0.70f, 0f, 0.06f)
            };
            Vector3[] rotations =
            {
                new Vector3(0f, -18f, 0f),
                new Vector3(0f, 12f, -8f),
                new Vector3(0f, -8f, -72f),
                new Vector3(0f, 17f, 0f),
                new Vector3(0f, -14f, 0f)
            };
            float[] targetSizes = { 0.30f, 0.36f, 0.50f, 0.38f, 0.30f };

            for (int i = 0; i < assetPaths.Length; i++)
            {
                AssetDatabase.ImportAsset(assetPaths[i], ImportAssetOptions.ForceSynchronousImport);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
                if (prefab == null)
                    throw new MissingReferenceException("Food Pack model was not found at " + assetPaths[i]);

                GameObject fruit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(fruit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                fruit.name = names[i];
                fruit.transform.SetParent(root.transform);
                fruit.transform.position = positions[i];
                fruit.transform.rotation = Quaternion.Euler(rotations[i]);

                Bounds bounds = GetCombinedRendererBounds(fruit);
                float longestSide = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longestSide > Mathf.Epsilon)
                    fruit.transform.localScale *= targetSizes[i] / longestSide;

                bounds = GetCombinedRendererBounds(fruit);
                fruit.transform.position += Vector3.up * (0.865f - bounds.min.y);

                foreach (MeshFilter filter in fruit.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;

                    MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = true;
                }

                Rigidbody body = fruit.AddComponent<Rigidbody>();
                body.mass = 0.25f;
                body.linearDamping = 0.5f;
                body.angularDamping = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                XRGrabInteractable grab = fruit.AddComponent<XRGrabInteractable>();
                grab.throwOnDetach = true;
            }
        }

        private static Bounds GetCombinedRendererBounds(GameObject gameObject)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(gameObject.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void CreateProjector(Material body, Material trim, Material lens)
        {
            GameObject root = new GameObject("Projector - Right Corner");
            root.transform.position = new Vector3(1.30f, 0.99f, 0.36f);

            CreateCube("Projector Body", root.transform, Vector3.zero, new Vector3(0.70f, 0.28f, 0.62f), body, true);
            CreateCube("Top Panel", root.transform, new Vector3(0f, 0.155f, -0.02f), new Vector3(0.54f, 0.035f, 0.44f), trim, false);
            CreateCylinder("Lens Housing", root.transform, new Vector3(0.19f, 0.015f, 0.34f), new Vector3(0.18f, 0.10f, 0.18f), new Vector3(90f, 0f, 0f), trim, false);
            CreateCylinder("Glass Lens", root.transform, new Vector3(0.19f, 0.015f, 0.405f), new Vector3(0.125f, 0.025f, 0.125f), new Vector3(90f, 0f, 0f), lens, false);
            CreateCube("Vent", root.transform, new Vector3(-0.36f, 0f, 0f), new Vector3(0.025f, 0.14f, 0.35f), trim, false);

            for (int i = 0; i < 3; i++)
                CreatePokeButton("Control Button " + (i + 1), root.transform, new Vector3(-0.16f + i * 0.13f, 0.19f, -0.08f), trim);
        }

        private static void CreatePokeButton(string name, Transform parent, Vector3 localPosition, Material material)
        {
            GameObject button = CreateCylinder(name, parent, localPosition, new Vector3(0.052f, 0.014f, 0.052f), Vector3.zero, material, true);
            Collider buttonCollider = button.GetComponent<Collider>();

            XRSimpleInteractable interactable = button.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(buttonCollider);

            XRPokeFilter pokeFilter = button.AddComponent<XRPokeFilter>();
            pokeFilter.pokeInteractable = interactable;
            pokeFilter.pokeCollider = buttonCollider;
            pokeFilter.pokeConfiguration = new PokeThresholdDatumProperty(new PokeThresholdData
            {
                pokeDirection = PokeAxis.NegativeY,
                interactionDepthOffset = 0.004f
            });
        }

        private static void CreatePortrait(Material portrait, Material frame)
        {
            GameObject root = new GameObject("Chancellor Diermeier Portrait");
            root.transform.position = new Vector3(-3.905f, 2.18f, 1.30f);

            GameObject image = GameObject.CreatePrimitive(PrimitiveType.Quad);
            image.name = "Realistic Portrait";
            image.transform.SetParent(root.transform);
            image.transform.localPosition = new Vector3(0.012f, 0f, 0f);
            image.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            image.transform.localScale = new Vector3(1.08f, 1.38f, 1f);
            image.GetComponent<MeshRenderer>().sharedMaterial = portrait;
            Object.DestroyImmediate(image.GetComponent<MeshCollider>());

            CreateCube("Frame Top", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.075f, 0.10f, 1.22f), frame, false);
            CreateCube("Frame Bottom", root.transform, new Vector3(0f, -0.75f, 0f), new Vector3(0.075f, 0.10f, 1.22f), frame, false);
            CreateCube("Frame Front", root.transform, new Vector3(0f, 0f, -0.61f), new Vector3(0.075f, 1.40f, 0.10f), frame, false);
            CreateCube("Frame Back", root.transform, new Vector3(0f, 0f, 0.61f), new Vector3(0.075f, 1.40f, 0.10f), frame, false);
        }

        private static void CreateLighting()
        {
            GameObject key = new GameObject("Soft Directional Light");
            Light directional = key.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(1f, 0.94f, 0.84f);
            directional.intensity = 1.25f;
            directional.shadows = LightShadows.Soft;
            directional.shadowStrength = 0.72f;
            key.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            CreatePointLight("Ceiling Fill Left", new Vector3(-2.2f, 3.65f, 0.2f), new Color(1f, 0.84f, 0.68f), 5.5f, 5.3f);
            CreatePointLight("Ceiling Fill Right", new Vector3(2.2f, 3.65f, 0.2f), new Color(0.75f, 0.86f, 1f), 4.2f, 5.3f);
        }

        private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreateXrRig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPath);
            if (prefab == null)
                throw new MissingReferenceException("XR Origin prefab was not found at " + XrRigPath);

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "XR Origin - Quest 3 Hands and Controllers";
            // Spawn at the table's front edge so fruit is within arm's reach without leaving the Guardian boundary.
            rig.transform.position = new Vector3(0f, 0f, -0.40f);
            rig.transform.rotation = Quaternion.identity;

            Camera camera = rig.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.06f, 0.065f, 0.075f);
            }
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            return go;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreatePortraitMaterial()
        {
            string path = GeneratedFolder + "/DiermeierPortrait.mat";
            AssetDatabase.DeleteAsset(path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath);
            if (texture == null)
                throw new MissingReferenceException("Portrait texture was not found at " + PortraitPath);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            Material material = new Material(shader) { name = "Diermeier Portrait" };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigurePortraitTexture()
        {
            AssetDatabase.ImportAsset(PortraitPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(PortraitPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string child = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
