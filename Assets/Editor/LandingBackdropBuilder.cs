using Projector.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // The room around the landing menu, styled as an old movie theater's concessions stand (see DESIGN.md): a wood
    // floor, a wooden counter, four instruction posters across the top of the booth where a menu board would be,
    // a popcorn machine popping behind the counter on the right, and a framed poster on the left wall with the game's
    // runners looping from the start line to the finish. The menus stand at the counter (the "Play Spot").
    // Re-running replaces the previous "Landing Backdrop".
    public static class LandingBackdropBuilder
    {
        private const string Folder = "Assets/Generated/Landing";
        private const string RootName = "Landing Backdrop";
        public const string PlaySpotName = "Play Spot";

        // The player stands at the origin facing +z. The room shell is the same as before.
        private static readonly Vector3 RoomCenter = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 RoomSize = new Vector3(10f, 4.5f, 12f);
        private const float CounterZ = 2.6f;
        private const float CounterTop = 1.05f;
        private const float HeaderZ = 3.9f;

        private static readonly Color PosterPaper = new Color(0.93f, 0.88f, 0.76f);
        private static readonly Color PosterInk = new Color(0.1f, 0.08f, 0.07f);
        private static readonly Color TheaterRed = new Color(0.45f, 0.07f, 0.07f);

        public static GameObject Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

            Material walls = CreateMaterial(Folder, "TheaterWall", new Color(0.09f, 0.095f, 0.11f), 0.1f, 0f);
            Material floor = CreateWoodMaterial(Folder, "TheaterFloor", new Vector2(RoomSize.x / 0.6f, RoomSize.z / 2.4f), Color.white);
            Material trim = CreateMaterial(Folder, "TheaterTrim", new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.2f);
            Material wood = CreateWoodMaterial(Folder, "CounterWood", new Vector2(12f, 0.45f), new Color(0.8f, 0.72f, 0.66f));
            Material counterTop = CreateWoodMaterial(Folder, "CounterTop", new Vector2(12f, 1f), new Color(0.5f, 0.42f, 0.38f), 0.12f);
            Material red = CreateMaterial(Folder, "BoothRed", new Color(0.32f, 0.05f, 0.06f), 0.2f, 0f);
            Material gold = CreateMaterial(Folder, "BoothGold", new Color(0.62f, 0.47f, 0.2f), 0.55f, 0.6f);

            var room = CreateRoomShell("Theater", RoomCenter, RoomSize, walls, floor);
            room.transform.SetParent(root.transform);

            // Warm and dim, from ambient and one unshadowed directional light: no extra per-pixel lights on Quest.
            SetFlatAmbient(new Color(0.34f, 0.3f, 0.27f));
            CreateDirectionalLight("Warm Fill", new Color(1f, 0.88f, 0.72f), 0.45f, new Vector3(50f, -20f, 0f)).transform.SetParent(root.transform);

            CreateCounter(root.transform, wood, counterTop);
            CreateBoothHeader(root.transform, red, gold);
            CreatePopcornMachine(root.transform, wood, trim, gold);
            CreateFramedPoster(root.transform, trim);
            CreatePlaySpot(root.transform);
            return root;
        }

        // A long wooden counter across the room, between the player and the booth, with a darker wood top.
        private static void CreateCounter(Transform parent, Material wood, Material top)
        {
            var counter = new GameObject("Counter");
            Undo.RegisterCreatedObjectUndo(counter, "Create Counter");
            counter.transform.SetParent(parent);
            counter.transform.position = new Vector3(0f, 0f, CounterZ);
            CreateCube("Body", counter.transform, new Vector3(0f, (CounterTop - 0.05f) / 2f, 0f), new Vector3(7f, CounterTop - 0.05f, 0.7f), wood, true);
            CreateCube("Top", counter.transform, new Vector3(0f, CounterTop - 0.025f, -0.03f), new Vector3(7.1f, 0.05f, 0.8f), top, true);
            CreateCube("Kick", counter.transform, new Vector3(0f, 0.05f, -0.36f), new Vector3(7f, 0.1f, 0.03f), top, false);
        }

        // The booth's header board, held up by two posts, with the four steps of a round on it as posters, left to
        // right, in place of a menu board.
        private static void CreateBoothHeader(Transform parent, Material red, Material gold)
        {
            var header = new GameObject("Booth Header");
            Undo.RegisterCreatedObjectUndo(header, "Create Booth Header");
            header.transform.SetParent(parent);
            header.transform.position = new Vector3(0f, 0f, HeaderZ);

            const float bottom = 2.35f, top = 3.75f, halfWidth = 4.1f;
            CreateCube("Board", header.transform, new Vector3(0f, (bottom + top) / 2f, 0f), new Vector3(halfWidth * 2f, top - bottom, 0.1f), red, false);
            CreateCube("Trim Top", header.transform, new Vector3(0f, top, -0.03f), new Vector3(halfWidth * 2f + 0.1f, 0.06f, 0.1f), gold, false);
            CreateCube("Trim Bottom", header.transform, new Vector3(0f, bottom, -0.03f), new Vector3(halfWidth * 2f + 0.1f, 0.06f, 0.1f), gold, false);
            CreateCube("Post Left", header.transform, new Vector3(-halfWidth, top / 2f, 0f), new Vector3(0.14f, top, 0.14f), red, false);
            CreateCube("Post Right", header.transform, new Vector3(halfWidth, top / 2f, 0f), new Vector3(0.14f, top, 0.14f), red, false);

            (string title, string body)[] steps =
            {
                ("BUILD", "Grab objects off the table and hold them in the projector's beam."),
                ("PROJECT", "Their shadows land on the screen as platforms, with a coin over each."),
                ("RACE", "Run and jump across the shadows. Grab coins, beat the others to the flag."),
                ("PLAY", "Host or join a game on the menu at the counter.")
            };
            const float posterWidth = 1.6f, gap = 0.3f;
            for (int i = 0; i < steps.Length; i++)
            {
                float x = (i - (steps.Length - 1) / 2f) * (posterWidth + gap);
                CreatePoster(header.transform, new Vector3(x, (bottom + top) / 2f, -0.06f), (i + 1).ToString(), steps[i].title, steps[i].body);
            }
        }

        // An old theater poster: cream paper in a red border, a big red step number, a title and one line of how-to.
        private static void CreatePoster(Transform parent, Vector3 localPosition, string number, string title, string body)
        {
            var canvas = CreateWorldCanvas("Poster " + number + " - " + title, parent, new Vector2(1000f, 750f), null);
            Object.DestroyImmediate(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            canvas.transform.localPosition = localPosition;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * 0.0016f;

            var border = CreateImage("Border", canvas.transform, TheaterRed);
            border.raycastTarget = false;
            SetFullSize(border.rectTransform);
            var paper = CreateImage("Paper", border.transform, PosterPaper);
            paper.raycastTarget = false;
            SetAnchors(paper.rectTransform, new Vector2(0.035f, 0.045f), new Vector2(0.965f, 0.955f));

            var badge = CreateText(number, paper.transform, 210, TheaterRed);
            SetAnchors(badge.rectTransform, new Vector2(0.04f, 0.5f), new Vector2(0.3f, 0.97f));
            var heading = CreateText(title, paper.transform, 120, PosterInk);
            heading.fontStyle = FontStyle.Bold;
            heading.alignment = TextAnchor.MiddleLeft;
            SetAnchors(heading.rectTransform, new Vector2(0.3f, 0.55f), new Vector2(0.97f, 0.92f));

            var text = CreateText(body, paper.transform, 62, PosterInk);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.UpperCenter;
            SetAnchors(text.rectTransform, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.5f));
        }

        // Behind the counter on the right, on a wooden shelf: a red popcorn cart with an open gold-framed case, a
        // kettle hanging from the roof, a pile of popcorn, and a few kernels popping out of the kettle at a time.
        private static void CreatePopcornMachine(Transform parent, Material wood, Material metal, Material gold)
        {
            Material red = CreateMaterial(Folder, "PopcornRed", new Color(0.6f, 0.08f, 0.07f), 0.45f, 0f);
            Material popcorn = CreateMaterial(Folder, "Popcorn", new Color(0.98f, 0.9f, 0.66f), 0.1f, 0f);
            Material kernel = CreateUnlitMaterial(Folder, "PopcornKernel", new Color(1f, 0.95f, 0.8f));

            const float shelfHeight = 0.95f;
            var position = new Vector3(2.5f, 0f, CounterZ + 0.85f);
            CreateCube("Popcorn Shelf", parent, position + new Vector3(0f, shelfHeight / 2f, 0f), new Vector3(1.3f, shelfHeight, 0.6f), wood, false);

            var machine = new GameObject("Popcorn Machine");
            Undo.RegisterCreatedObjectUndo(machine, "Create Popcorn Machine");
            machine.transform.SetParent(parent);
            machine.transform.position = position + Vector3.up * shelfHeight;

            CreateCube("Base", machine.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.3f, 0.5f), red, false);
            CreateCube("Roof", machine.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.76f, 0.1f, 0.56f), red, false);
            for (int i = 0; i < 4; i++)
                CreateCube("Post", machine.transform, new Vector3(i % 2 == 0 ? -0.335f : 0.335f, 0.65f, i < 2 ? -0.235f : 0.235f), new Vector3(0.03f, 0.7f, 0.03f), gold, false);
            CreateCylinder("Kettle Rod", machine.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.015f, 0.05f, 0.015f), Vector3.zero, metal, false);
            CreateCylinder("Kettle", machine.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.22f, 0.06f, 0.22f), Vector3.zero, metal, false);
            var pile = CreatePrimitive(PrimitiveType.Sphere, "Popcorn Pile", machine.transform, new Vector3(0f, 0.32f, 0f), new Vector3(0.6f, 0.14f, 0.42f), Vector3.zero, popcorn, false);

            // Quiet popping: a handful of kernels alive at once, drawn as tiny unlit spheres with no shadows.
            var popping = new GameObject("Popping");
            Undo.RegisterCreatedObjectUndo(popping, "Create Popping");
            popping.transform.SetParent(machine.transform);
            popping.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.9f, 0f), Quaternion.Euler(-90f, 0f, 0f));
            var particles = popping.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.75f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.035f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 6f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;
            var particleRenderer = popping.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = pile.GetComponent<MeshFilter>().sharedMesh;
            particleRenderer.sharedMaterial = kernel;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        // On the left wall: a framed black-and-white poster, "PROJECTOR" across the top, with the runners hopping
        // along a shadow ground from the start line to the finish flag and reappearing back at the start.
        private static void CreateFramedPoster(Transform parent, Material frame)
        {
            Material paper = CreateUnlitMaterial(Folder, "PosterPaper", new Color(0.8f, 0.8f, 0.78f));
            Material ink = CreateUnlitMaterial(Folder, "PosterInk", new Color(0.12f, 0.12f, 0.12f));

            var poster = new GameObject("Framed Poster");
            Undo.RegisterCreatedObjectUndo(poster, "Create Framed Poster");
            poster.transform.SetParent(parent);
            // Facing into the room: local +x runs toward the front of the room, local -z toward the viewer.
            poster.transform.SetPositionAndRotation(new Vector3(RoomCenter.x - RoomSize.x / 2f + 0.12f, 1.9f, 0.6f), Quaternion.LookRotation(Vector3.left));

            const float width = 1.5f, height = 1.05f, bar = 0.08f;
            CreateCube("Paper", poster.transform, Vector3.zero, new Vector3(width, height, 0.01f), paper, false);
            CreateCube("Frame Top", poster.transform, new Vector3(0f, (height + bar) / 2f, -0.01f), new Vector3(width + bar * 2f, bar, 0.05f), frame, false);
            CreateCube("Frame Bottom", poster.transform, new Vector3(0f, -(height + bar) / 2f, -0.01f), new Vector3(width + bar * 2f, bar, 0.05f), frame, false);
            CreateCube("Frame Left", poster.transform, new Vector3(-(width + bar) / 2f, 0f, -0.01f), new Vector3(bar, height, 0.05f), frame, false);
            CreateCube("Frame Right", poster.transform, new Vector3((width + bar) / 2f, 0f, -0.01f), new Vector3(bar, height, 0.05f), frame, false);

            var title = CreateWorldCanvas("Title", poster.transform, new Vector2(1400f, 220f), null);
            Object.DestroyImmediate(title.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            title.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.36f, -0.01f), Quaternion.identity);
            var titleText = CreateText("PROJECTOR", title.transform, 190, new Color(0.08f, 0.08f, 0.08f));
            titleText.fontStyle = FontStyle.Bold;
            SetFullSize(titleText.rectTransform);

            const float startX = -0.62f, finishX = 0.6f, groundY = -0.33f;
            CreateCube("Ground", poster.transform, new Vector3(0f, groundY, -0.01f), new Vector3(1.36f, 0.035f, 0.005f), ink, false);
            CreateCube("Start Line", poster.transform, new Vector3(startX, groundY + 0.1f, -0.01f), new Vector3(0.01f, 0.2f, 0.005f), ink, false);
            CreateCube("Finish Pole", poster.transform, new Vector3(finishX + 0.02f, groundY + 0.15f, -0.01f), new Vector3(0.012f, 0.3f, 0.005f), ink, false);
            CreateCube("Finish Flag", poster.transform, new Vector3(finishX - 0.04f, groundY + 0.26f, -0.012f), new Vector3(0.11f, 0.07f, 0.005f), ink, false);

            // Three runners in shades of gray, spread out along the track and at slightly different paces.
            float[] shades = { 0.18f, 0.32f, 0.46f };
            float[] starts = { -0.5f, -0.05f, 0.35f };
            float[] speeds = { 0.2f, 0.17f, 0.23f };
            for (int i = 0; i < shades.Length; i++)
            {
                Material body = CreateUnlitMaterial(Folder, "PosterRunner" + i, new Color(shades[i], shades[i], shades[i]));
                var runner = CreatePrimitive(PrimitiveType.Capsule, "Runner " + (i + 1), poster.transform,
                    new Vector3(starts[i], groundY + 0.1f, -0.015f), new Vector3(0.07f, 0.08f, 0.005f), Vector3.zero, body, false);
                var loop = runner.AddComponent<PosterRunner>();
                SetFloat(loop, "startX", startX);
                SetFloat(loop, "finishX", finishX);
                SetFloat(loop, "speed", speeds[i]);
            }
        }

        // The menus stand here, in front of the middle of the counter (see LandingMenu's menu anchor).
        private static void CreatePlaySpot(Transform parent)
        {
            var spot = new GameObject(PlaySpotName);
            Undo.RegisterCreatedObjectUndo(spot, "Create " + PlaySpotName);
            spot.transform.SetParent(parent);
            spot.transform.SetPositionAndRotation(new Vector3(0f, 1.62f, CounterZ - 0.55f), Quaternion.identity);
        }
    }
}
