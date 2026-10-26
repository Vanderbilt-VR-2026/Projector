using Projector.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // The room around the landing menu, so the first thing players see explains the game: a dark projector
    // theater whose wall screen shows a level drawn from shadows, with runners racing across it; the table of
    // props that cast those shadows; the projector behind the player throwing its beam; and three cards for the
    // steps (build, project, race), then the menus themselves as step 4, to the player's right.
    // Re-running replaces the previous "Landing Backdrop".
    public static class LandingBackdropBuilder
    {
        private const string Folder = "Assets/Generated/Landing";
        private const string RootName = "Landing Backdrop";
        public const string PlaySpotName = "Play Spot";

        // Room interior: the player stands at the origin facing +z, the screen is on the front wall.
        private const float FrontWallZ = 7f;
        private static readonly Rect Screen = new Rect(-4.2f, 0.7f, 8.4f, 3.4f);
        private static readonly Vector3 Lens = new Vector3(0f, 2.35f, -4.3f);
        private const float TableTop = 0.8f;

        public static GameObject Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

            Material walls = CreateMaterial(Folder, "TheaterWall", new Color(0.09f, 0.095f, 0.11f), 0.1f, 0f);
            Material floor = CreateMaterial(Folder, "TheaterFloor", new Color(0.13f, 0.12f, 0.12f), 0.2f, 0f);
            Material trim = CreateMaterial(Folder, "TheaterTrim", new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.2f);
            Material wood = CreateMaterial(Folder, "TableWood", new Color(0.42f, 0.28f, 0.17f), 0.3f, 0f);

            var room = CreateRoomShell("Theater", new Vector3(0f, 0f, 1f), new Vector3(10f, 4.5f, 12f), walls, floor);
            room.transform.SetParent(root.transform);

            SetFlatAmbient(new Color(0.32f, 0.33f, 0.38f));
            CreateDirectionalLight("Soft Fill", new Color(0.85f, 0.88f, 1f), 0.35f, new Vector3(55f, 20f, 0f)).transform.SetParent(root.transform);

            CreateScreen(root.transform, trim);
            CreateProjectedLevel(root.transform);
            CreateProjector(root.transform, trim);
            CreateTable(root.transform, wood);
            CreateStepCards(root.transform);
            return root;
        }

        // A bright wall screen in a dark frame, lit from the projector.
        private static void CreateScreen(Transform parent, Material frame)
        {
            Material surface = CreateUnlitMaterial(Folder, "ScreenSurface", new Color(0.95f, 0.92f, 0.84f));
            float z = FrontWallZ - 0.1f;
            CreateCube("Projection Screen", parent, new Vector3(Screen.center.x, Screen.center.y, z), new Vector3(Screen.width, Screen.height, 0.02f), surface, false);

            const float bar = 0.08f;
            CreateCube("Frame Top", parent, new Vector3(0f, Screen.yMax, z), new Vector3(Screen.width + bar, bar, 0.04f), frame, false);
            CreateCube("Frame Bottom", parent, new Vector3(0f, Screen.yMin, z), new Vector3(Screen.width + bar, bar, 0.04f), frame, false);
            CreateCube("Frame Left", parent, new Vector3(Screen.xMin, Screen.center.y, z), new Vector3(bar, Screen.height, 0.04f), frame, false);
            CreateCube("Frame Right", parent, new Vector3(Screen.xMax, Screen.center.y, z), new Vector3(bar, Screen.height, 0.04f), frame, false);
        }

        // What the game projects: the props' shadows as platforms (same shapes as the table props), a coin over
        // each, the four runners mid-race and the finish flag.
        private static void CreateProjectedLevel(Transform parent)
        {
            Material shadow = CreateUnlitMaterial(Folder, "ProjectedShadow", new Color(0.1f, 0.1f, 0.12f));
            Material coin = CreateUnlitMaterial(Folder, "ProjectedCoin", AccentColor);
            float z = FrontWallZ - 0.13f;

            var level = new GameObject("Projected Level");
            Undo.RegisterCreatedObjectUndo(level, "Create Projected Level");
            level.transform.SetParent(parent);

            // (shape, center on the screen, size): plank, block, slab, pillar, ball, rod.
            (PrimitiveType type, Vector2 center, Vector2 size)[] platforms =
            {
                (PrimitiveType.Cube, new Vector2(-3.4f, 1.05f), new Vector2(1.3f, 0.16f)),
                (PrimitiveType.Cube, new Vector2(-2.35f, 1.55f), new Vector2(0.45f, 0.45f)),
                (PrimitiveType.Cube, new Vector2(-1.5f, 1.85f), new Vector2(0.9f, 0.22f)),
                (PrimitiveType.Cube, new Vector2(0f, 1.3f), new Vector2(0.3f, 1.0f)),
                (PrimitiveType.Sphere, new Vector2(1.3f, 2.1f), new Vector2(0.5f, 0.5f)),
                (PrimitiveType.Cube, new Vector2(2.4f, 1.6f), new Vector2(1.2f, 0.12f)),
                (PrimitiveType.Cube, new Vector2(3.45f, 1.1f), new Vector2(1.0f, 0.18f))
            };
            foreach (var platform in platforms)
            {
                CreatePrimitive(platform.type, "Shadow Platform", level.transform, new Vector3(platform.center.x, platform.center.y, z),
                    new Vector3(platform.size.x, platform.size.y, 0.01f), Vector3.zero, shadow, false);
                var coinObject = CreateCylinder("Coin", level.transform, new Vector3(platform.center.x, platform.center.y + platform.size.y / 2f + 0.32f, z - 0.03f),
                    new Vector3(0.16f, 0.015f, 0.16f), new Vector3(90f, 0f, 0f), coin, false);
                // Spins in place (no collider, so nothing can collect it).
                coinObject.AddComponent<Coin>();
            }

            // Runners on the left (the start) and right (closing in on the flag), a couple mid-jump.
            Vector2[] runners = { new Vector2(-3.75f, 1.38f), new Vector2(-3.1f, 1.62f), new Vector2(2.1f, 1.92f), new Vector2(2.85f, 2.25f) };
            for (int slot = 0; slot < runners.Length; slot++)
            {
                Material body = CreateUnlitMaterial(Folder, "ProjectedRunner" + slot, PlayerColors.Get(slot));
                var runner = CreatePrimitive(PrimitiveType.Capsule, "Runner " + (slot + 1), level.transform,
                    new Vector3(runners[slot].x, runners[slot].y, z - 0.02f), new Vector3(0.2f, 0.25f, 0.01f), Vector3.zero, body, false);
                CreatePrimitive(PrimitiveType.Capsule, "Outline", runner.transform, new Vector3(0f, 0f, 0.5f), new Vector3(1.25f, 1.15f, 1f), Vector3.zero, shadow, false);
            }

            Material flag = CreateUnlitMaterial(Folder, "ProjectedFlag", PlayerColors.Get(0));
            CreateCube("Finish Pole", level.transform, new Vector3(3.85f, 1.55f, z - 0.01f), new Vector3(0.04f, 0.75f, 0.01f), shadow, false);
            CreateCube("Finish Flag", level.transform, new Vector3(3.69f, 1.8f, z - 0.02f), new Vector3(0.3f, 0.2f, 0.01f), flag, false);

            var banner = CreateWorldCanvas("Screen Banner", level.transform, new Vector2(4000f, 300f), null);
            banner.transform.SetPositionAndRotation(new Vector3(0f, Screen.yMax - 0.22f, z - 0.03f), Quaternion.identity);
            Object.DestroyImmediate(banner.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            SetFullSize(CreateText("PROJECTOR: WHERE 3D MEETS 2D", banner.transform, 190, new Color(0.1f, 0.1f, 0.12f)).rectTransform);
        }

        // Behind the player, high up like a cinema projector, so its beam passes overhead onto the screen.
        private static void CreateProjector(Transform parent, Material trim)
        {
            Material body = CreateMaterial(Folder, "ProjectorBody", new Color(0.82f, 0.82f, 0.85f), 0.45f, 0.1f);
            Material glass = CreateUnlitMaterial(Folder, "ProjectorLens", new Color(1f, 0.97f, 0.88f));

            var projector = new GameObject("Projector");
            Undo.RegisterCreatedObjectUndo(projector, "Create Projector");
            projector.transform.SetParent(parent);
            projector.transform.position = Lens + new Vector3(0f, -0.02f, -0.36f);
            CreateCube("Shelf", projector.transform, new Vector3(0f, -0.2f, -0.05f), new Vector3(1.1f, 0.06f, 0.8f), trim, false);
            CreateCube("Body", projector.transform, Vector3.zero, new Vector3(0.62f, 0.28f, 0.7f), body, false);
            CreateCylinder("Lens Housing", projector.transform, new Vector3(0f, 0.02f, 0.38f), new Vector3(0.2f, 0.08f, 0.2f), new Vector3(90f, 0f, 0f), trim, false);
            CreateCylinder("Glass Lens", projector.transform, new Vector3(0f, 0.02f, 0.44f), new Vector3(0.15f, 0.02f, 0.15f), new Vector3(90f, 0f, 0f), glass, false);

            float z = FrontWallZ - 0.12f;
            var corners = new[]
            {
                new Vector3(Screen.xMin, Screen.yMin, z), new Vector3(Screen.xMax, Screen.yMin, z),
                new Vector3(Screen.xMax, Screen.yMax, z), new Vector3(Screen.xMin, Screen.yMax, z)
            };
            CreateBeam("Projector Beam", parent, Lens, corners, Folder + "/LandingBeam.asset");

            var light = new GameObject("Projector Light").AddComponent<Light>();
            Undo.RegisterCreatedObjectUndo(light.gameObject, "Create Projector Light");
            light.transform.SetParent(parent);
            light.transform.SetPositionAndRotation(Lens, Quaternion.LookRotation(new Vector3(0f, Screen.center.y, z) - Lens));
            light.type = LightType.Spot;
            light.spotAngle = 50f;
            light.innerSpotAngle = 30f;
            light.range = 14f;
            light.intensity = 4f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None;
        }

        // The objects players arrange in the beam: the same six shapes and colors as the projector room's props.
        private static void CreateTable(Transform parent, Material wood)
        {
            var table = new GameObject("Prop Table");
            Undo.RegisterCreatedObjectUndo(table, "Create Prop Table");
            table.transform.SetParent(parent);
            table.transform.SetPositionAndRotation(new Vector3(-2.3f, 0f, 2.1f), Quaternion.Euler(0f, -45f, 0f));
            CreateCube("Top", table.transform, new Vector3(0f, TableTop - 0.03f, 0f), new Vector3(1.8f, 0.06f, 0.8f), wood, false);
            for (int i = 0; i < 4; i++)
                CreateCube("Leg", table.transform, new Vector3(i % 2 == 0 ? -0.82f : 0.82f, (TableTop - 0.06f) / 2f, i < 2 ? -0.32f : 0.32f), new Vector3(0.06f, TableTop - 0.06f, 0.06f), wood, false);

            (string name, PrimitiveType type, Vector3 scale, Vector3 euler, Color color)[] props =
            {
                ("Block", PrimitiveType.Cube, new Vector3(0.14f, 0.14f, 0.14f), new Vector3(0f, 20f, 0f), new Color(0.85f, 0.45f, 0.35f)),
                ("Plank", PrimitiveType.Cube, new Vector3(0.5f, 0.03f, 0.1f), Vector3.zero, new Color(0.75f, 0.6f, 0.4f)),
                ("Pillar", PrimitiveType.Cylinder, new Vector3(0.07f, 0.15f, 0.07f), Vector3.zero, new Color(0.45f, 0.6f, 0.85f)),
                ("Ball", PrimitiveType.Sphere, new Vector3(0.13f, 0.13f, 0.13f), Vector3.zero, new Color(0.5f, 0.8f, 0.5f)),
                ("Slab", PrimitiveType.Cube, new Vector3(0.3f, 0.05f, 0.2f), new Vector3(0f, -10f, 0f), new Color(0.7f, 0.7f, 0.75f)),
                ("Rod", PrimitiveType.Cylinder, new Vector3(0.04f, 0.3f, 0.04f), new Vector3(0f, 0f, 90f), new Color(0.9f, 0.75f, 0.3f))
            };
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                Material material = CreateMaterial(Folder, "Prop" + prop.name, prop.color, 0.3f, 0f);
                float x = (i - (props.Length - 1) / 2f) * 0.28f;
                var go = CreatePrimitive(prop.type, prop.name, table.transform, new Vector3(x, TableTop + 0.2f, i % 2 == 0 ? -0.12f : 0.12f), prop.scale, prop.euler, material, false);
                var bounds = go.GetComponent<Renderer>().bounds;
                go.transform.position += Vector3.up * (TableTop - bounds.min.y);
            }
        }

        // The steps of a round, left to right around the player: 1 over the table, 2 on the screen (between the
        // level and the banner), 3 on the right, and 4 where the menus stand (see LandingMenu's menu anchor).
        private static void CreateStepCards(Transform parent)
        {
            CreateStepCard(parent, AroundPlayer(-45f, 3.1f, 1.75f), 0.0013f, "1", "BUILD", "Grab objects off the table and hold them in the projector's beam.");
            CreateStepCard(parent, new Pose(new Vector3(0f, 2.95f, FrontWallZ - 0.16f), Quaternion.identity), 0.0026f, "2", "PROJECT", "Their shadows are cast on the wall as platforms, with a coin over each.");
            CreateStepCard(parent, AroundPlayer(45f, 3.1f, 1.75f), 0.0013f, "3", "RACE", "Run and jump across the shadows. Grab coins, beat the others to the flag.");
            CreatePlaySpot(parent);
        }

        // Step 4: the menus stand here, and a "4 PLAY!" heading sits above them.
        private static void CreatePlaySpot(Transform parent)
        {
            var pose = AroundPlayer(88f, 1.8f, 1.45f);
            var spot = new GameObject(PlaySpotName);
            Undo.RegisterCreatedObjectUndo(spot, "Create " + PlaySpotName);
            spot.transform.SetParent(parent);
            spot.transform.SetPositionAndRotation(pose.position, pose.rotation);

            var heading = CreateWorldCanvas("Step 4 - PLAY", spot.transform, new Vector2(900f, 160f), null);
            Object.DestroyImmediate(heading.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            heading.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            var number = CreateText("4", heading.transform, 130, AccentColor);
            SetAnchors(number.rectTransform, new Vector2(0f, 0f), new Vector2(0.35f, 1f));
            number.alignment = TextAnchor.MiddleRight;
            var title = CreateText("PLAY!", heading.transform, 110, Color.white);
            SetAnchors(title.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 1f));
            title.alignment = TextAnchor.MiddleLeft;
        }

        // Facing the player from `yaw` degrees right of straight ahead.
        private static Pose AroundPlayer(float yaw, float distance, float height)
        {
            var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return new Pose(direction * distance + Vector3.up * height, Quaternion.LookRotation(direction));
        }

        private static void CreateStepCard(Transform parent, Pose pose, float scale, string number, string title, string body)
        {
            var canvas = CreateWorldCanvas("Step " + number + " - " + title, parent, new Vector2(900f, 560f), null);
            Object.DestroyImmediate(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            canvas.transform.SetPositionAndRotation(pose.position, pose.rotation);
            canvas.transform.localScale = Vector3.one * scale;

            var panel = CreatePanel("Panel", canvas.transform);
            panel.GetComponent<Image>().raycastTarget = false;

            var badge = CreateText(number, panel, 150, AccentColor);
            SetAnchors(badge.rectTransform, new Vector2(0.05f, 0.5f), new Vector2(0.28f, 0.95f));
            var heading = CreateText(title, panel, 92, Color.white);
            heading.alignment = TextAnchor.MiddleLeft;
            SetAnchors(heading.rectTransform, new Vector2(0.3f, 0.55f), new Vector2(0.95f, 0.92f));

            var text = CreateText(body, panel, 46, new Color(0.82f, 0.86f, 0.92f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.UpperLeft;
            SetAnchors(text.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.5f));
        }
    }
}
