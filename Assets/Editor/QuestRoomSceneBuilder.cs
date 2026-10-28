using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // The projector room as an old movie theater (see DESIGN.md): a raised wooden stage holding the table and an
    // old-school reel projector on a black stand, the projection screen on the front wall between dark red
    // curtains tied back with gold rope, and rows of dark red seats facing the stage that fade into black (fog,
    // no side or back walls). The stage edges are invisible walls, so players stay on it.
    // ProjectorRoomGameplayBuilder adds the screen image, the pull rope and the game on top.
    public static class QuestRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ProjectorRoom.unity";
        private const string GeneratedFolder = "Assets/Generated/QuestRoom";

        // The front wall's inner face is x = -3.92 (ProjectionCapture.wallX); the screen on it spans z -2.3..15.7,
        // y 1.1..7.1 (ProjectionCapture.screen). The players' floor (the stage) is y = 0.
        private const float WallX = -4f;
        private const float ScreenMinZ = -2.3f;
        private const float ScreenMaxZ = 15.7f;
        private const float ScreenTop = 7.1f;
        private const float CeilingY = 13.5f;
        private const float StageFrontX = 6.5f;
        private const float StageMinZ = -7f;
        private const float StageMaxZ = 19f;
        private const float AudienceFloorY = -1f;

        [MenuItem("Projector/Build Quest Room Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(GeneratedFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Projector Room";

            Material wall = CreateMaterial("TheaterWall", new Color(0.05f, 0.045f, 0.045f), 0.05f, 0f);
            Material carpet = CreateMaterial("Carpet", new Color(0.09f, 0.03f, 0.03f), 0.02f, 0f);
            Material stage = CreateWoodMaterial(GeneratedFolder, "StageFloor", new Vector2(10.5f / 0.6f, (StageMaxZ - StageMinZ) / 2.4f), Color.white);
            Material seat = CreateMaterial("SeatRed", new Color(0.3f, 0.04f, 0.05f), 0.15f, 0f);
            Material curtain = CreateMaterial("CurtainRed", new Color(0.36f, 0.04f, 0.05f), 0.25f, 0f);
            curtain.SetFloat("_Cull", (float)CullMode.Off);
            Material gold = CreateMaterial("DarkGold", new Color(0.45f, 0.32f, 0.1f), 0.5f, 0.6f);
            Material wood = CreateMaterial("WalnutTable", new Color(0.24f, 0.095f, 0.035f), 0.30f, 0f);
            Material metal = CreateMaterial("DarkMetal", new Color(0.055f, 0.06f, 0.07f), 0.55f, 0.75f);
            Material projectorBody = CreateMaterial("ProjectorBody", new Color(0.16f, 0.16f, 0.15f), 0.5f, 0.6f);
            Material lens = CreateMaterial("ProjectorLens", new Color(0.045f, 0.075f, 0.105f), 0.75f, 0.35f);
            Material aluminum = CreateMaterial("BrushedAluminum", new Color(0.66f, 0.68f, 0.70f), 0.55f, 0.85f);

            CreateRoom(wall, carpet, stage);
            CreateStageBounds();
            CreateSeats(seat);
            CreateCurtains(curtain, gold);
            CreateTable(wood, metal);
            CreateProjector(projectorBody, metal, lens, aluminum);
            CreateLighting();
            CreateXrRig();

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
            cameraObject.transform.position = new Vector3(5.5f, 1.8f, -1.5f);
            cameraObject.transform.LookAt(new Vector3(-3.9f, 3.2f, 6.7f));
            previewCamera.fieldOfView = 70f;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 50f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.black;
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

        // The front wall (behind the screen), the stage in front of it one meter above the audience floor, and a
        // ceiling three times the old height. Nothing closes in the audience: the fog takes it to black.
        private static void CreateRoom(Material wall, Material carpet, Material stage)
        {
            GameObject root = new GameObject("Room");
            const float minZ = -12f, maxZ = 26f, backX = 34f;
            float width = maxZ - minZ, centerZ = (minZ + maxZ) / 2f, depth = backX - WallX, centerX = (WallX + backX) / 2f;
            CreateCube("Audience Floor", root.transform, new Vector3(centerX, AudienceFloorY - 0.08f, centerZ), new Vector3(depth, 0.16f, width), carpet, true);
            CreateCube("Front Wall", root.transform, new Vector3(WallX, (AudienceFloorY + CeilingY) / 2f, centerZ), new Vector3(0.16f, CeilingY - AudienceFloorY, width), wall, true);
            CreateCube("Ceiling", root.transform, new Vector3(centerX, CeilingY, centerZ), new Vector3(depth, 0.16f, width), wall, false);
            CreateCube("Stage", root.transform, new Vector3((WallX + StageFrontX) / 2f, AudienceFloorY / 2f, (StageMinZ + StageMaxZ) / 2f),
                new Vector3(StageFrontX - WallX, -AudienceFloorY, StageMaxZ - StageMinZ), stage, true);
        }

        // Invisible walls along the stage's open edges (the front wall closes the back): the rig's character
        // controller stops at them. On Ignore Raycast so they never catch a controller ray.
        private static void CreateStageBounds()
        {
            var root = new GameObject("Stage Bounds");
            const float height = 3f, thickness = 0.2f;
            float centerZ = (StageMinZ + StageMaxZ) / 2f, length = StageMaxZ - StageMinZ, centerX = (WallX + StageFrontX) / 2f, depth = StageFrontX - WallX;
            CreateBound(root.transform, "Front Edge", new Vector3(StageFrontX + thickness / 2f, height / 2f, centerZ), new Vector3(thickness, height, length));
            CreateBound(root.transform, "Left Edge", new Vector3(centerX, height / 2f, StageMinZ - thickness / 2f), new Vector3(depth, height, thickness));
            CreateBound(root.transform, "Right Edge", new Vector3(centerX, height / 2f, StageMaxZ + thickness / 2f), new Vector3(depth, height, thickness));
        }

        private static void CreateBound(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var bound = new GameObject(name) { layer = 2 };
            bound.transform.SetParent(parent);
            bound.transform.position = position;
            bound.AddComponent<BoxCollider>().size = size;
        }

        // Rows of seats facing the stage, combined into one mesh (one draw call), with an aisle down the middle.
        private static void CreateSeats(Material material)
        {
            const int rows = 20, columns = 50;
            const float firstRowX = StageFrontX + 2f, rowSpacing = 1.05f, seatSpacing = 0.6f;
            float centerZ = (ScreenMinZ + ScreenMaxZ) / 2f;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);

            var parts = new List<CombineInstance>();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (column == columns / 2 - 1 || column == columns / 2)
                        continue;
                    var seat = new Vector3(firstRowX + row * rowSpacing, AudienceFloorY, centerZ + (column - (columns - 1) / 2f) * seatSpacing);
                    // Cushion, then the back on the far side from the stage (seats face -x).
                    parts.Add(new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(seat + new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.48f, 0.12f, 0.5f)) });
                    parts.Add(new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(seat + new Vector3(0.25f, 0.72f, 0f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.08f, 0.62f, 0.5f)) });
                }
            }

            Mesh mesh = LoadOrCreateMesh(GeneratedFolder + "/Seats.asset", "Seats");
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var seats = new GameObject("Seats");
            seats.AddComponent<MeshFilter>().sharedMesh = mesh;
            var seatRenderer = seats.AddComponent<MeshRenderer>();
            seatRenderer.sharedMaterial = material;
            seatRenderer.shadowCastingMode = ShadowCastingMode.Off;
            seatRenderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(seats, StaticEditorFlags.BatchingStatic);
        }

        // A curtain on either side of the screen, gathered toward the outside and tied with gold rope, and a
        // valance across the top.
        private static void CreateCurtains(Material curtain, Material rope)
        {
            const float width = 2f, height = 8.6f, tieHeight = 2.4f, x = WallX + 0.3f;
            Mesh mesh = CreateCurtainMesh(width, height, tieHeight);

            var root = new GameObject("Curtains");
            // Each curtain's outer edge is its local x = 0; local +x runs toward the screen.
            (string name, float outerZ, float yaw)[] sides = { ("Left Curtain", ScreenMinZ - 0.15f - width, -90f), ("Right Curtain", ScreenMaxZ + 0.15f + width, 90f) };
            foreach (var side in sides)
            {
                var drape = new GameObject(side.name);
                drape.transform.SetParent(root.transform);
                drape.transform.SetPositionAndRotation(new Vector3(x, 0f, side.outerZ), Quaternion.Euler(0f, side.yaw, 0f));
                drape.AddComponent<MeshFilter>().sharedMesh = mesh;
                var drapeRenderer = drape.AddComponent<MeshRenderer>();
                drapeRenderer.sharedMaterial = curtain;
                drapeRenderer.shadowCastingMode = ShadowCastingMode.Off;

                float gathered = width * 0.3f;
                CreateCylinder("Tie Rope", drape.transform, new Vector3(gathered / 2f, tieHeight, 0f), new Vector3(gathered + 0.12f, 0.035f, 0.3f), Vector3.zero, rope, false);
                CreateCylinder("Rope Tail", drape.transform, new Vector3(gathered + 0.04f, tieHeight - 0.3f, -0.12f), new Vector3(0.03f, 0.3f, 0.03f), Vector3.zero, rope, false);
            }

            float valanceLength = ScreenMaxZ - ScreenMinZ + 2f * (width + 0.15f);
            CreateCube("Valance", root.transform, new Vector3(x + 0.12f, ScreenTop + 0.9f, (ScreenMinZ + ScreenMaxZ) / 2f), new Vector3(0.12f, 1.2f, valanceLength), curtain, false);
            CreateCube("Valance Trim", root.transform, new Vector3(x + 0.19f, ScreenTop + 0.32f, (ScreenMinZ + ScreenMaxZ) / 2f), new Vector3(0.03f, 0.05f, valanceLength), rope, false);
        }

        // A pleated sheet, full width at the top, pinched to 30% at the tie and flaring a little below it.
        private static Mesh CreateCurtainMesh(float width, float height, float tieHeight)
        {
            const int columns = 24, rows = 14, pleats = 7;
            const float depth = 0.08f;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int r = 0; r <= rows; r++)
            {
                float y = height * r / rows;
                float spread = y >= tieHeight
                    ? Mathf.Lerp(0.3f, 1f, Mathf.Pow((y - tieHeight) / (height - tieHeight), 0.7f))
                    : Mathf.Lerp(0.3f, 0.45f, (tieHeight - y) / tieHeight);
                for (int c = 0; c <= columns; c++)
                {
                    float u = c / (float)columns;
                    float fold = Mathf.Sin(u * pleats * 2f * Mathf.PI) * depth * (1.3f - spread * 0.5f);
                    vertices[r * (columns + 1) + c] = new Vector3(u * width * spread, y, fold);
                    uvs[r * (columns + 1) + c] = new Vector2(u, y / height);
                }
            }
            var triangles = new int[columns * rows * 6];
            for (int r = 0, t = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++, t += 6)
                {
                    int i = r * (columns + 1) + c;
                    triangles[t] = i;
                    triangles[t + 1] = i + columns + 1;
                    triangles[t + 2] = i + 1;
                    triangles[t + 3] = i + 1;
                    triangles[t + 4] = i + columns + 1;
                    triangles[t + 5] = i + columns + 2;
                }
            }

            Mesh mesh = LoadOrCreateMesh(GeneratedFolder + "/Curtain.asset", "Curtain");
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh LoadOrCreateMesh(string path, string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            return mesh;
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

        // An old reel-to-reel projector on a black stand at the end of the table, lens toward the screen; the
        // buttons on top stay at hand height.
        private static void CreateProjector(Material body, Material stand, Material lens, Material reel)
        {
            GameObject root = new GameObject("Projector");
            root.transform.position = new Vector3(4.2f, 1.30f, 0.55f);
            root.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            CreateCube("Stand Top", root.transform, new Vector3(0f, -0.17f, -0.05f), new Vector3(0.5f, 0.04f, 0.7f), stand, true);
            CreateCube("Stand Column", root.transform, new Vector3(0f, -0.7f, -0.05f), new Vector3(0.1f, 1.02f, 0.1f), stand, true);
            CreateCube("Stand Base", root.transform, new Vector3(0f, -1.28f, -0.05f), new Vector3(0.6f, 0.04f, 0.6f), stand, true);

            CreateCube("Projector Body", root.transform, Vector3.zero, new Vector3(0.42f, 0.3f, 0.6f), body, true);
            CreateCylinder("Lens Housing", root.transform, new Vector3(0f, 0.02f, 0.36f), new Vector3(0.16f, 0.12f, 0.16f), new Vector3(90f, 0f, 0f), stand, false);
            CreateCylinder("Glass Lens", root.transform, new Vector3(0f, 0.02f, 0.49f), new Vector3(0.11f, 0.015f, 0.11f), new Vector3(90f, 0f, 0f), lens, false);

            // Feed reel in front, take-up reel behind, each on an arm above the body.
            (string name, float z, float y)[] reels = { ("Feed Reel", 0.2f, 0.5f), ("Take-up Reel", -0.28f, 0.55f) };
            foreach (var r in reels)
            {
                CreateCube(r.name + " Arm", root.transform, new Vector3(0.16f, (0.15f + r.y) / 2f, r.z), new Vector3(0.025f, r.y - 0.15f, 0.03f), stand, false);
                CreateCylinder(r.name, root.transform, new Vector3(0.12f, r.y, r.z), new Vector3(0.44f, 0.02f, 0.44f), new Vector3(0f, 0f, 90f), reel, false);
                CreateCylinder(r.name + " Hub", root.transform, new Vector3(0.12f, r.y, r.z), new Vector3(0.08f, 0.04f, 0.08f), new Vector3(0f, 0f, 90f), stand, false);
            }

            for (int i = 0; i < 3; i++)
                CreatePokeButton("Control Button " + (i + 1), root.transform, new Vector3(-0.12f + i * 0.12f, 0.165f, -0.04f), stand);
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

        // Dim and warm: low ambient, one soft directional wash over the stage from the audience side, no shadows
        // and no extra per-pixel lights (the projector's own spot is added with the gameplay). Black linear fog
        // fades the seats out with distance, and reflections are black so nothing picks up a sky tint.
        private static void CreateLighting()
        {
            CreateDirectionalLight("Stage Wash", new Color(1f, 0.86f, 0.7f), 0.4f, new Vector3(40f, -90f, 0f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.095f, 0.09f);
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 32f;
        }

        private static void CreateXrRig()
        {
            // Spawn at the table's front edge so the items are within arm's reach.
            SceneBuildUtility.CreateXrRig("XR Origin - Quest 3 Hands and Controllers", new Vector3(0f, 0f, -0.40f), Quaternion.identity, Color.black);
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            return SceneBuildUtility.CreateMaterial(GeneratedFolder, name, color, smoothness, metallic);
        }
    }
}
