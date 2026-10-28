using Projector.Gameplay;
using GameplayProjectorGame = Projector.Gameplay.ProjectorGame;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // Adds the whole game to the projector room (QuestRoomSceneBuilder's theater) without rebuilding the room.
    // The projector films an off-screen 2D "stage" with an orthographic camera and shows it on the screen on the
    // front wall through the projection shader; props in the beam are drawn on that stage as 2D shapes, and the
    // race runs there too. A rope hanging beside the screen starts and ends rounds, with what it does written in a
    // square of light on the wall. Re-running replaces the previous "Gameplay" objects.
    public static class ProjectorRoomGameplayBuilder
    {
        private const string ScenePath = "Assets/Scenes/ProjectorRoom.unity";
        private const string GrayboxFolder = "Assets/Generated/Graybox";
        private const string ProjectionFolder = "Assets/Generated/Projection";
        private const string RootName = "Gameplay";
        private const float TableTop = 0.865f;
        private const float CeilingY = 13.5f;
        // Where the old menu stood, just past the left curtain: the pull rope, and the square of light beyond it.
        private static readonly Vector3 RopePosition = new Vector3(-3.3f, 0f, -4.9f);
        private static readonly Vector3 LightSquareCenter = new Vector3(-3.9f, 2.2f, -6f);
        private static readonly Color Ink = new Color(0.12f, 0.11f, 0.1f);
        // Far below the room and outside the headset camera's 50 m far plane, so only the projector camera sees it.
        private static readonly Vector3 StageOrigin = new Vector3(0f, -500f, 0f);

        [MenuItem("Projector/Add Gameplay To Projector Room")]
        public static void AddGameplay()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var previous = GameObject.Find(RootName);
            if (previous != null)
                Object.DestroyImmediate(previous);

            var root = new GameObject(RootName);
            ProjectionCapture capture = CreateProjector(root.transform);
            CreateScreenFrame(root.transform, capture);

            Transform stage = CreateStage(root.transform, out ProjectedPlatforms platforms, out Scoreboard scoreboard, out Text banner);
            RenderTexture projection = CreateProjectorCamera(stage);
            CreateProjectionScreen(root.transform, capture, projection);
            CreateBeam(root.transform, capture);
            CreateGame(root.transform, capture, platforms, scoreboard, banner, stage);

            var spawner = CreatePlayerSpawner(NetworkPrefabsBuilder.AvatarPath, TableSpots());
            spawner.transform.SetParent(root.transform);
            GameObject.Find("Offline Host").transform.SetParent(root.transform);

            SaveNetworkScene(scene, ScenePath);
            Debug.Log("Gameplay added to " + ScenePath);
        }

        // Players stand along the front of the table, facing the room.
        private static Vector3[] TableSpots()
        {
            var spots = new Vector3[PlayerColors.All.Length];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = new Vector3((i - (spots.Length - 1) / 2f) * 0.8f, 0f, -0.4f);
            return spots;
        }

        // During the race everyone stands back beside the projector, side by side, facing the screen, within a
        // controller ray's reach of the rope.
        private static Object[] CreateViewingSpots(Transform parent, ProjectionCapture capture)
        {
            var screenCenter = new Vector3(capture.WallX, capture.Screen.center.y, capture.Screen.center.x);
            var spots = new Object[PlayerColors.All.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = new GameObject($"Viewing Spot {i + 1}").transform;
                spot.SetParent(parent);
                spot.position = new Vector3(5f, 0f, -3.6f + i * 0.6f);
                var toScreen = screenCenter - spot.position;
                toScreen.y = 0f;
                spot.rotation = Quaternion.LookRotation(toScreen);
                spots[i] = spot;
            }
            return spots;
        }

        // The projector's light: it lights props hanging in the beam but fades out before the wall,
        // so the projected image (not shadows) is what shows there.
        private static ProjectionCapture CreateProjector(Transform parent)
        {
            var lens = GameObject.Find("Glass Lens");
            if (lens == null)
                throw new MissingReferenceException("Projector 'Glass Lens' not found in " + ScenePath);

            var beam = new GameObject("Projector Lens");
            beam.transform.SetParent(parent);
            beam.transform.position = lens.transform.position + Vector3.left * 0.03f;
            var capture = beam.AddComponent<ProjectionCapture>();
            // Tilted up at the middle of the screen, which sits above the table so everyone can see all of it.
            var screenCenter = new Vector3(capture.WallX, capture.Screen.center.y, capture.Screen.center.x);
            beam.transform.rotation = Quaternion.LookRotation(screenCenter - beam.transform.position);

            var light = beam.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 100f;
            light.innerSpotAngle = 70f;
            light.range = 7f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.None;
            return capture;
        }

        // Black masking around the projected image, like a cinema screen's border.
        private static void CreateScreenFrame(Transform parent, ProjectionCapture capture)
        {
            Material frame = CreateMaterial(ProjectionFolder, "ScreenMasking", new Color(0.02f, 0.02f, 0.02f), 0.1f, 0f);
            var root = new GameObject("Screen Frame");
            root.transform.SetParent(parent);
            Rect screen = capture.Screen;
            float x = capture.WallX + 0.01f;
            const float bar = 0.15f;
            CreateCube("Top", root.transform, new Vector3(x, screen.yMax, screen.center.x), new Vector3(0.02f, bar, screen.width + bar), frame, false);
            CreateCube("Bottom", root.transform, new Vector3(x, screen.yMin, screen.center.x), new Vector3(0.02f, bar, screen.width + bar), frame, false);
            CreateCube("Left", root.transform, new Vector3(x, screen.center.y, screen.xMin), new Vector3(0.02f, screen.height, bar), frame, false);
            CreateCube("Right", root.transform, new Vector3(x, screen.center.y, screen.xMax), new Vector3(0.02f, screen.height, bar), frame, false);
        }

        // Simple shapes that project into useful platforms: long ones become runways, tall ones become walls.
        private static void CreateProps(Transform parent)
        {
            var root = new GameObject("Props");
            root.transform.SetParent(parent);

            (string name, PrimitiveType type, Vector3 scale, Vector3 euler, Color color)[] props =
            {
                ("Block", PrimitiveType.Cube, new Vector3(0.14f, 0.14f, 0.14f), Vector3.zero, new Color(0.85f, 0.45f, 0.35f)),
                ("Plank", PrimitiveType.Cube, new Vector3(0.1f, 0.03f, 0.5f), Vector3.zero, new Color(0.75f, 0.6f, 0.4f)),
                ("Pillar", PrimitiveType.Cylinder, new Vector3(0.07f, 0.15f, 0.07f), Vector3.zero, new Color(0.45f, 0.6f, 0.85f)),
                ("Ball", PrimitiveType.Sphere, new Vector3(0.13f, 0.13f, 0.13f), Vector3.zero, new Color(0.5f, 0.8f, 0.5f)),
                ("Slab", PrimitiveType.Cube, new Vector3(0.2f, 0.05f, 0.3f), Vector3.zero, new Color(0.7f, 0.7f, 0.75f)),
                ("Rod", PrimitiveType.Cylinder, new Vector3(0.04f, 0.3f, 0.04f), new Vector3(90f, 0f, 0f), new Color(0.9f, 0.75f, 0.3f))
            };

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                Material material = CreateMaterial(GrayboxFolder, "Prop" + prop.name, prop.color, 0.3f, 0f);
                float x = (i - (props.Length - 1) / 2f) * 0.45f;
                var go = CreatePrimitive(prop.type, prop.name, root.transform, new Vector3(x, TableTop + 0.2f, 0.15f), prop.scale, prop.euler, material, true);
                // Rest it on the table.
                var bounds = go.GetComponent<Renderer>().bounds;
                go.transform.position += Vector3.up * (TableTop - bounds.min.y);
                MakeNetworkProp(go);
            }
        }

        private static void MakeNetworkProp(GameObject go)
        {
            // Explicit checks: Unity's fake-null objects break ?? on components.
            var body = go.GetComponent<Rigidbody>();
            if (body == null)
                body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = go.GetComponent<XRGrabInteractable>();
            if (grab == null)
                grab = go.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            // XR grabbing re-parents the object while held; positions sync in world space, so skip parent sync
            // (which would reject the move as invalid parenting).
            var networkObject = go.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = false;
            AddOwnerNetworkTransform(go);
            go.AddComponent<NetworkProp>();
        }

        // The 2D world the projector shows: level coordinates are the stage's local x/y, with the level
        // spanning x -15..15 and y 0..10 to match the screen's 3:1 frame.
        private static Transform CreateStage(Transform parent, out ProjectedPlatforms platforms, out Scoreboard scoreboard, out Text banner)
        {
            var stage = new GameObject("Projection Stage").transform;
            stage.SetParent(parent);
            stage.position = StageOrigin;

            Material shapes = CreateUnlitMaterial(ProjectionFolder, "StageShape", Color.white);
            platforms = stage.gameObject.AddComponent<ProjectedPlatforms>();
            SetReference(platforms, "shapeMaterial", shapes);
            SetReference(platforms, "coinMaterial", shapes);

            // Written on the platforms themselves, clear of the runners' name tags.
            CreateStageLabel(stage, "START", new Vector2(-13.5f, 1.5f), 0.0055f, null, new Color(0.05f, 0.2f, 0.08f));
            CreateStageLabel(stage, "FINISH", new Vector2(13.5f, 1.5f), 0.0055f, null, new Color(0.3f, 0.2f, 0.02f));
            Material pole = CreateUnlitMaterial(ProjectionFolder, "StagePole", new Color(0.85f, 0.85f, 0.85f));
            Material flag = CreateUnlitMaterial(ProjectionFolder, "StageFlag", new Color(0.9f, 0.25f, 0.25f));
            CreateCube("Flag Pole", stage, new Vector3(14.6f, 3.1f, 0.02f), new Vector3(0.08f, 2.2f, 0.01f), pole, false);
            CreateCube("Flag", stage, new Vector3(14.2f, 3.95f, 0.01f), new Vector3(0.75f, 0.5f, 0.01f), flag, false);

            // Dark, like everything else drawn on the off-white film.
            banner = CreateStageLabel(stage, "", new Vector2(0f, 6.5f), 0.025f, "Countdown", Ink);

            // Results, drawn over the level when the race ends.
            Canvas results = CreateWorldCanvas("Results", stage, new Vector2(1200f, 700f), null);
            results.transform.localPosition = new Vector3(0f, 5.2f, -0.5f);
            results.transform.localScale = Vector3.one * 0.011f;
            scoreboard = results.gameObject.AddComponent<Scoreboard>();
            var panel = CreatePanel("Panel", results.transform);
            var title = CreateText("RESULTS", panel, 72, Color.white);
            SetAnchors(title.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f));
            var winner = CreateText("", panel, 52, AccentColor, "Winner");
            SetAnchors(winner.rectTransform, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.83f));
            string[] headers = { "#", "PLAYER", "POINTS", "TIME" };
            string[] fields = { "rankColumn", "nameColumn", "pointsColumn", "timeColumn" };
            float[] columnEdges = { 0.05f, 0.15f, 0.55f, 0.75f, 0.95f };
            for (int i = 0; i < headers.Length; i++)
            {
                var header = CreateText(headers[i], panel, 38, new Color(0.7f, 0.75f, 0.8f));
                SetAnchors(header.rectTransform, new Vector2(columnEdges[i], 0.58f), new Vector2(columnEdges[i + 1], 0.67f));
                var column = CreateText("", panel, 50, Color.white, headers[i] + " Column");
                column.alignment = TextAnchor.UpperCenter;
                column.lineSpacing = 1.25f;
                SetAnchors(column.rectTransform, new Vector2(columnEdges[i], 0.05f), new Vector2(columnEdges[i + 1], 0.57f));
                SetReference(scoreboard, fields[i], column);
            }
            SetReference(scoreboard, "winnerText", winner);
            SetBool(scoreboard, "showMockOnStart", false);
            results.gameObject.SetActive(false);
            return stage;
        }

        private static Text CreateStageLabel(Transform stage, string text, Vector2 position, float scale = 0.008f, string name = null, Color? color = null)
        {
            Canvas canvas = CreateWorldCanvas((name ?? text) + " Label", stage, new Vector2(800f, 200f), null);
            // Just in front of the platforms (the projector camera looks along +z).
            canvas.transform.localPosition = new Vector3(position.x, position.y, -0.1f);
            canvas.transform.localScale = Vector3.one * scale;
            Text label = CreateText(text, canvas.transform, 120, color ?? Color.white, name);
            SetFullSize(label.rectTransform);
            return label;
        }

        // Orthographic camera that films the stage into the texture the wall shows.
        private static RenderTexture CreateProjectorCamera(Transform stage)
        {
            EnsureFolder(ProjectionFolder);
            const string texturePath = ProjectionFolder + "/Projection.renderTexture";
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (texture == null)
            {
                texture = new RenderTexture(1536, 512, 16, RenderTextureFormat.ARGB32) { name = "Projection" };
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            // Mips feed the screen shader's soft focus and glow.
            texture.Release();
            texture.width = 1536;
            texture.height = 512;
            texture.antiAliasing = 2;
            texture.useMipMap = true;
            texture.autoGenerateMips = true;
            texture.filterMode = FilterMode.Trilinear;
            EditorUtility.SetDirty(texture);

            var cameraObject = new GameObject("Projector Camera");
            cameraObject.transform.SetParent(stage, false);
            cameraObject.transform.localPosition = new Vector3(0f, 5f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Off-white film: the screen shader adds the grain and mutes the colors.
            camera.backgroundColor = new Color(0.9f, 0.88f, 0.82f);
            camera.targetTexture = texture;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.allowHDR = false;
            return texture;
        }

        // The image on the wall, inside the frame.
        private static void CreateProjectionScreen(Transform parent, ProjectionCapture capture, RenderTexture projection)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ProjectionFolder + "/ProjectionScreen.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Projector/Projection Screen"));
                AssetDatabase.CreateAsset(material, ProjectionFolder + "/ProjectionScreen.mat");
            }
            material.SetTexture("_MainTex", projection);
            // Grain from URP's own film grain textures. The film is light, so it needs little extra brightness or
            // bloom, and it covers the screen fully.
            material.SetTexture("_Grain", AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.render-pipelines.universal/Textures/FilmGrain/Medium02.png"));
            material.SetFloat("_Brightness", 1.05f);
            material.SetFloat("_Bloom", 0.12f);
            material.SetFloat("_Wash", 0f);
            material.SetFloat("_Ambient", 0f);
            material.SetFloat("_Scanlines", 0.04f);
            material.SetFloat("_GrainStrength", 0.45f);
            EditorUtility.SetDirty(material);

            Rect screen = capture.Screen;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Projection Screen";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent);
            // Faces into the room; quad u runs along +z (the viewer's right) to match the projector camera.
            quad.transform.SetPositionAndRotation(new Vector3(capture.WallX + 0.006f, screen.center.y, screen.center.x), Quaternion.LookRotation(Vector3.left));
            quad.transform.localScale = new Vector3(screen.width, screen.height, 1f);
            var quadRenderer = quad.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
        }

        // A faint pyramid of light from the lens to the screen corners.
        private static void CreateBeam(Transform parent, ProjectionCapture capture)
        {
            Rect screen = capture.Screen;
            float x = capture.WallX + 0.02f;
            var corners = new[]
            {
                new Vector3(x, screen.yMin, screen.xMin), new Vector3(x, screen.yMin, screen.xMax),
                new Vector3(x, screen.yMax, screen.xMax), new Vector3(x, screen.yMax, screen.xMin)
            };
            SceneBuildUtility.CreateBeam("Projector Beam", parent, capture.transform.position, corners, ProjectionFolder + "/ProjectorBeam.asset");
        }

        // The game loop object, its race, and its controls: the pull rope and the instructions in the light.
        private static void CreateGame(Transform parent, ProjectionCapture capture, ProjectedPlatforms platforms, Scoreboard scoreboard, Text banner, Transform stage)
        {
            var root = new GameObject("Projector Game");
            root.transform.SetParent(parent);
            root.AddComponent<NetworkObject>();
            GameplayProjectorGame game = root.AddComponent<GameplayProjectorGame>();
            RaceManager race = root.AddComponent<RaceManager>();

            PullRope rope = CreatePullRope(root.transform);
            Text hud = CreateLightSquare(root.transform);

            SetReference(game, "capture", capture);
            SetReference(game, "platforms", platforms);
            SetReference(game, "race", race);
            SetReference(game, "scoreboard", scoreboard);
            SetReference(game, "hudText", hud);
            SetReference(game, "rope", rope);
            SetReferences(game, "itemPrefabs", new Object[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Block.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Plank.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/L-Block.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Stairs.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Ramp.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Arch.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Trampoline.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Spike Strip.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Buzzsaw.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/NetworkPrefabs/ItemMocks/Coin.prefab")
            });
            SetReferences(game, "viewingSpots", CreateViewingSpots(root.transform, capture));
            var projectorButton = GameObject.Find("Control Button 1");
            if (projectorButton != null)
                SetReference(game, "projectorButton", projectorButton.GetComponent<XRSimpleInteractable>());

            var runner = AssetDatabase.LoadAssetAtPath<NetworkObject>(NetworkPrefabsBuilder.RunnerPath);
            SetReference(race, "runnerPrefab", runner);
            SetReference(race, "stage", stage);
            SetReference(race, "game", game);
            SetReference(race, "banner", banner);
            EnsureEventSystem();
        }

        // A thick gold rope hanging from the ceiling with a tassel at hand height. Grabbing it anywhere (up close or
        // with a ray) and pulling down works the game (see ProjectorGame).
        private static PullRope CreatePullRope(Transform parent)
        {
            Material cord = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/QuestRoom/DarkGold.mat");
            Material tassel = CreateMaterial(ProjectionFolder, "Tassel", new Color(0.62f, 0.45f, 0.16f), 0.3f, 0.2f);

            var root = new GameObject("Pull Rope");
            root.transform.SetParent(parent);
            root.transform.position = new Vector3(RopePosition.x, CeilingY, RopePosition.z);
            var rope = new GameObject("Rope").transform;
            rope.SetParent(root.transform, false);

            const float bottom = 1.25f;
            float length = CeilingY - bottom;
            CreateCylinder("Cord", rope, new Vector3(0f, -length / 2f, 0f), new Vector3(0.06f, length / 2f, 0.06f), Vector3.zero, cord, true);
            CreatePrimitive(PrimitiveType.Sphere, "Knob", rope, new Vector3(0f, -length - 0.04f, 0f), Vector3.one * 0.13f, Vector3.zero, tassel, true);
            CreateCylinder("Tassel", rope, new Vector3(0f, -length - 0.2f, 0f), new Vector3(0.15f, 0.11f, 0.15f), Vector3.zero, tassel, true);
            foreach (var part in rope.GetComponentsInChildren<Renderer>())
                part.shadowCastingMode = ShadowCastingMode.Off;

            root.AddComponent<XRSimpleInteractable>();
            var pull = root.AddComponent<PullRope>();
            SetReference(pull, "rope", rope);
            return pull;
        }

        // A square of warm light on the wall with soft edges, with the rope's instructions on it in black.
        private static Text CreateLightSquare(Transform parent)
        {
            Material material = CreateGlowMaterial(ProjectionFolder, "LightSquare", new Color(1f, 0.92f, 0.76f), 0.75f);
            Mesh mesh = CreateSoftRectMesh(ProjectionFolder + "/LightSquare.asset", 1.3f, 1.3f, 0.2f);

            // Facing into the room (local -z toward the viewer).
            var square = new GameObject("Light Square");
            square.transform.SetParent(parent);
            square.transform.SetPositionAndRotation(LightSquareCenter, Quaternion.LookRotation(Vector3.left));
            square.AddComponent<MeshFilter>().sharedMesh = mesh;
            var squareRenderer = square.AddComponent<MeshRenderer>();
            squareRenderer.sharedMaterial = material;
            squareRenderer.shadowCastingMode = ShadowCastingMode.Off;
            squareRenderer.receiveShadows = false;

            Canvas canvas = CreateWorldCanvas("Instructions", square.transform, new Vector2(1000f, 1000f), null);
            Object.DestroyImmediate(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.0012f;
            Text text = CreateText("Pull to start", canvas.transform, 130, Color.black, "Status");
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.fontStyle = FontStyle.Bold;
            SetFullSize(text.rectTransform);
            return text;
        }
    }
}
