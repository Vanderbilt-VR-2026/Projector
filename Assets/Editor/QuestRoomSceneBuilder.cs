using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Editor
{
    public static class QuestRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GeneratedFolder = "Assets/Generated/QuestRoom";
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
            Material banana = CreateMaterial("BananaYellow", new Color(1f, 0.66f, 0.025f), 0.12f, 0f);
            Material bananaTip = CreateMaterial("BananaTip", new Color(0.20f, 0.085f, 0.02f), 0.08f, 0f);
            Material frame = CreateMaterial("PortraitFrame", new Color(0.24f, 0.15f, 0.055f), 0.35f, 0.1f);
            Material portrait = CreatePortraitMaterial();

            CreateRoom(whiteWall, sideWall, floor);
            CreateTable(wood, metal);
            CreateBananas(banana, bananaTip);
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

        private static void CreateBananas(Material bananaMaterial, Material tipMaterial)
        {
            Mesh mesh = CreateBananaMesh();
            string meshPath = GeneratedFolder + "/BananaMesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject root = new GameObject("Five Big Bananas");
            Vector3[] positions =
            {
                new Vector3(-1.72f, 0.99f, 0.25f),
                new Vector3(-0.88f, 1.00f, 0.55f),
                new Vector3(-0.05f, 1.00f, 0.18f),
                new Vector3(0.78f, 1.00f, 0.48f),
                new Vector3(1.45f, 1.00f, 0.10f)
            };
            float[] yaw = { -18f, 12f, -8f, 17f, -14f };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject banana = new GameObject("Big Banana " + (i + 1));
                banana.transform.SetParent(root.transform);
                banana.transform.position = positions[i];
                banana.transform.rotation = Quaternion.Euler(0f, yaw[i], i % 2 == 0 ? -5f : 4f);
                banana.transform.localScale = Vector3.one * 1.18f;

                MeshFilter filter = banana.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = banana.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = bananaMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;

                MeshCollider collider = banana.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
                Rigidbody body = banana.AddComponent<Rigidbody>();
                body.mass = 0.25f;
                body.linearDamping = 0.5f;
                body.angularDamping = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                XRGrabInteractable grab = banana.AddComponent<XRGrabInteractable>();
                grab.throwOnDetach = true;

                CreateSphere("Stem Tip", banana.transform, new Vector3(0.36f, 0.14f, 0f), 0.055f, tipMaterial, false);
                CreateSphere("Blossom Tip", banana.transform, new Vector3(-0.36f, 0.14f, 0f), 0.045f, tipMaterial, false);
            }
        }

        private static Mesh CreateBananaMesh()
        {
            const int lengthSegments = 20;
            const int radialSegments = 10;
            const float majorRadius = 0.43f;
            const float tubeRadius = 0.115f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= lengthSegments; i++)
            {
                float t = i / (float)lengthSegments;
                float angle = Mathf.Lerp(-62f, 62f, t) * Mathf.Deg2Rad;
                Vector3 center = new Vector3(Mathf.Sin(angle) * majorRadius, (1f - Mathf.Cos(angle)) * majorRadius, 0f);
                Vector3 inPlaneNormal = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f).normalized;
                Vector3 binormal = Vector3.forward;
                float taper = Mathf.SmoothStep(0.30f, 1f, Mathf.Min(t * 5f, (1f - t) * 5f));
                float radius = tubeRadius * taper;

                for (int j = 0; j < radialSegments; j++)
                {
                    float ringAngle = j * Mathf.PI * 2f / radialSegments;
                    Vector3 normal = inPlaneNormal * Mathf.Cos(ringAngle) + binormal * Mathf.Sin(ringAngle);
                    vertices.Add(center + normal * radius);
                    normals.Add(normal);
                    uvs.Add(new Vector2(t, j / (float)radialSegments));
                }
            }

            for (int i = 0; i < lengthSegments; i++)
            {
                for (int j = 0; j < radialSegments; j++)
                {
                    int next = (j + 1) % radialSegments;
                    int a = i * radialSegments + j;
                    int b = i * radialSegments + next;
                    int c = (i + 1) * radialSegments + j;
                    int d = (i + 1) * radialSegments + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = "Quest Optimized Banana" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateProjector(Material body, Material trim, Material lens)
        {
            GameObject root = new GameObject("Projector - Right Corner");
            root.transform.position = new Vector3(2.00f, 0.99f, 0.72f);

            CreateCube("Projector Body", root.transform, Vector3.zero, new Vector3(0.70f, 0.28f, 0.62f), body, true);
            CreateCube("Top Panel", root.transform, new Vector3(0f, 0.155f, -0.02f), new Vector3(0.54f, 0.035f, 0.44f), trim, false);
            CreateCylinder("Lens Housing", root.transform, new Vector3(0.19f, 0.015f, 0.34f), new Vector3(0.18f, 0.10f, 0.18f), new Vector3(90f, 0f, 0f), trim, false);
            CreateCylinder("Glass Lens", root.transform, new Vector3(0.19f, 0.015f, 0.405f), new Vector3(0.125f, 0.025f, 0.125f), new Vector3(90f, 0f, 0f), lens, false);
            CreateCube("Vent", root.transform, new Vector3(-0.36f, 0f, 0f), new Vector3(0.025f, 0.14f, 0.35f), trim, false);

            for (int i = 0; i < 3; i++)
                CreateCylinder("Control Button " + (i + 1), root.transform, new Vector3(-0.16f + i * 0.13f, 0.19f, -0.08f), new Vector3(0.038f, 0.012f, 0.038f), Vector3.zero, trim, false);
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
            rig.transform.position = new Vector3(0f, 0f, -2.65f);
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

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, float diameter, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<SphereCollider>());
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
