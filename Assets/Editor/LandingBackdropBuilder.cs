using System.Collections.Generic;
using Projector.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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
            Material popcorn = CreateMaterial(Folder, "Popcorn", new Color(0.98f, 0.9f, 0.66f), 0.1f, 0f);
            popcorn.enableInstancing = true;
            CreatePopcornMachine(root.transform, wood, trim, gold, popcorn);
            CreateFloorPopcorn(root.transform, popcorn);
            CreateFramedPoster(root.transform, counterTop, gold, red);
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
            const float posterWidth = 1.6f, posterHeight = 1.2f, gap = 0.3f;
            var centers = new Vector3[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                centers[i] = new Vector3((i - (steps.Length - 1) / 2f) * (posterWidth + gap), (bottom + top) / 2f, -0.06f);
                CreatePoster(header.transform, centers[i], (i + 1).ToString(), steps[i].title, steps[i].body);
            }
            CreateMarqueeLights(header.transform, centers, posterWidth, posterHeight);
        }

        // Marquee bulbs around each poster, with a warm glow on the board behind it. No real lights: the bulbs are
        // unlit, the glow is an additive soft-edged rectangle, and each is one combined mesh for all four posters.
        private static void CreateMarqueeLights(Transform parent, Vector3[] posterCenters, float posterWidth, float posterHeight)
        {
            Material bulb = CreateUnlitMaterial(Folder, "MarqueeBulb", new Color(1f, 0.86f, 0.55f));
            Material glow = CreateGlowMaterial(Folder, "PosterGlow", new Color(1f, 0.72f, 0.38f), 0.4f);
            // Before the posters' canvases (queue 3000), so the glow only lights the board around them.
            glow.renderQueue = 2990;
            Mesh bulbMesh = CreateRoundedBoxMesh(Folder + "/Bulb.asset", 2, 0.5f);
            Mesh glowMesh = CreateSoftRectMesh(Folder + "/PosterGlow.asset", posterWidth + 0.2f, posterHeight + 0.2f, 0.18f);

            const float inset = 0.1f, spacing = 0.15f, size = 0.05f;
            float halfWidth = posterWidth / 2f + inset, halfHeight = posterHeight / 2f + inset;
            var bulbs = new List<CombineInstance>();
            var glows = new List<CombineInstance>();
            foreach (var center in posterCenters)
            {
                glows.Add(new CombineInstance { mesh = glowMesh, transform = Matrix4x4.Translate(center + new Vector3(0f, 0f, 0.008f)) });
                int across = Mathf.RoundToInt(halfWidth * 2f / spacing), down = Mathf.RoundToInt(halfHeight * 2f / spacing);
                for (int i = 0; i <= across; i++)
                {
                    float x = -halfWidth + i * halfWidth * 2f / across;
                    AddBulb(bulbs, bulbMesh, center + new Vector3(x, halfHeight, -0.04f), size);
                    AddBulb(bulbs, bulbMesh, center + new Vector3(x, -halfHeight, -0.04f), size);
                }
                for (int j = 1; j < down; j++)
                {
                    float y = -halfHeight + j * halfHeight * 2f / down;
                    AddBulb(bulbs, bulbMesh, center + new Vector3(-halfWidth, y, -0.04f), size);
                    AddBulb(bulbs, bulbMesh, center + new Vector3(halfWidth, y, -0.04f), size);
                }
            }
            CreateMeshObject("Poster Glow", parent, CombineIntoMesh(Folder + "/PosterGlows.asset", glows), glow);
            CreateMeshObject("Marquee Bulbs", parent, CombineIntoMesh(Folder + "/MarqueeBulbs.asset", bulbs), bulb);
        }

        private static void AddBulb(List<CombineInstance> bulbs, Mesh mesh, Vector3 position, float size)
        {
            bulbs.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * size) });
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
            badge.font = HeaderFont;
            SetAnchors(badge.rectTransform, new Vector2(0.04f, 0.5f), new Vector2(0.3f, 0.97f));
            var heading = CreateText(title, paper.transform, 120, PosterInk);
            heading.font = HeaderFont;
            heading.alignment = TextAnchor.MiddleLeft;
            SetAnchors(heading.rectTransform, new Vector2(0.3f, 0.55f), new Vector2(0.97f, 0.92f));

            var text = CreateText(body, paper.transform, 62, PosterInk);
            text.font = BodyFont;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.UpperCenter;
            SetAnchors(text.rectTransform, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.5f));
        }

        // Behind the counter on the right, on a wooden shelf: a red popcorn cart with an open gold-framed case, a
        // kettle hanging from the roof, a pile of popcorn, and a few kernels popping out of the kettle at a time.
        private static void CreatePopcornMachine(Transform parent, Material wood, Material metal, Material gold, Material popcorn)
        {
            Material red = CreateMaterial(Folder, "PopcornRed", new Color(0.6f, 0.08f, 0.07f), 0.45f, 0f);
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

        // Spilled popcorn on the floor between the player and the counter, heaviest below the machine, that players
        // can pick up and throw around. Local physics only (the landing room isn't networked): each piece is a lumpy
        // low-poly kernel with a sphere collider, a light rigidbody and a grab interactable that throws on release.
        private static void CreateFloorPopcorn(Transform parent, Material popcorn)
        {
            Mesh ball = CreateRoundedBoxMesh(Folder + "/Bulb.asset", 2, 0.5f);
            var lumps = new List<CombineInstance>
            {
                new CombineInstance { mesh = ball, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 0.6f) },
                new CombineInstance { mesh = ball, transform = Matrix4x4.TRS(new Vector3(0.25f, 0.12f, 0.05f), Quaternion.identity, Vector3.one * 0.5f) },
                new CombineInstance { mesh = ball, transform = Matrix4x4.TRS(new Vector3(-0.2f, 0.15f, 0.12f), Quaternion.identity, Vector3.one * 0.45f) },
                new CombineInstance { mesh = ball, transform = Matrix4x4.TRS(new Vector3(0.05f, -0.1f, -0.25f), Quaternion.identity, Vector3.one * 0.45f) }
            };
            Mesh kernel = CombineIntoMesh(Folder + "/PopcornPiece.asset", lumps);

            var root = new GameObject("Floor Popcorn");
            Undo.RegisterCreatedObjectUndo(root, "Create Floor Popcorn");
            root.transform.SetParent(parent);

            // Bigger than real popcorn so it's easy to see and grab in VR (about 7 cm), scattered in front of the
            // counter and heaped below the machine.
            const int pieces = 15;
            const float size = 0.07f;
            var random = new System.Random(11);
            for (int i = 0; i < pieces; i++)
            {
                bool underMachine = i < 5;
                float x = underMachine ? 2.5f + Rand(random, -0.6f, 0.6f) : Rand(random, -3.2f, 3.2f);
                float z = underMachine ? CounterZ - 0.5f + Rand(random, -0.25f, 0.2f) : Rand(random, 0.5f, CounterZ - 0.45f);
                var piece = CreateMeshObject("Popcorn", root.transform, kernel, popcorn);
                piece.transform.SetPositionAndRotation(new Vector3(x, size * 0.5f, z), Quaternion.Euler(Rand(random, 0f, 360f), Rand(random, 0f, 360f), Rand(random, 0f, 360f)));
                piece.transform.localScale = Vector3.one * size;

                piece.AddComponent<SphereCollider>().radius = 0.5f;
                var body = piece.AddComponent<Rigidbody>();
                body.mass = 0.02f;
                body.linearDamping = 0.3f;
                body.angularDamping = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var grab = piece.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
            }
        }

        private static float Rand(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // On the left wall: a framed black-and-white poster, "PROJECTOR" across the top over "NOW IN THEATERS", with
        // the runners hopping along a shadow ground from the start line to the finish flag and reappearing back at
        // the start.
        private static void CreateFramedPoster(Transform parent, Material frame, Material gold, Material velvet)
        {
            Material paper = CreateUnlitMaterial(Folder, "PosterPaper", new Color(0.8f, 0.8f, 0.78f));
            Material ink = CreateUnlitMaterial(Folder, "PosterInk", new Color(0.12f, 0.12f, 0.12f));

            var poster = new GameObject("Framed Poster");
            Undo.RegisterCreatedObjectUndo(poster, "Create Framed Poster");
            poster.transform.SetParent(parent);
            // Facing into the room: local +x runs toward the front of the room, local -z toward the viewer.
            poster.transform.SetPositionAndRotation(new Vector3(RoomCenter.x - RoomSize.x / 2f + 0.12f, 1.9f, 0.6f), Quaternion.LookRotation(Vector3.left));

            const float width = 1.5f, height = 1.05f;
            CreateCube("Paper", poster.transform, Vector3.zero, new Vector3(width, height, 0.01f), paper, false);
            CreateGildedFrame(poster.transform, width, height, frame, gold, velvet);

            var title = CreateWorldCanvas("Title", poster.transform, new Vector2(1400f, 220f), null);
            Object.DestroyImmediate(title.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            title.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.36f, -0.01f), Quaternion.identity);
            var titleText = CreateText("PROJECTOR", title.transform, 190, new Color(0.08f, 0.08f, 0.08f));
            titleText.font = HeaderFont;
            SetFullSize(titleText.rectTransform);

            var tagline = CreateWorldCanvas("Tagline", poster.transform, new Vector2(1400f, 120f), null);
            Object.DestroyImmediate(tagline.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            tagline.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.2f, -0.01f), Quaternion.identity);
            var taglineText = CreateText("NOW IN THEATERS", tagline.transform, 80, new Color(0.2f, 0.2f, 0.2f));
            taglineText.font = BodyFont;
            SetFullSize(taglineText.rectTransform);

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

        // A gilded picture frame around a width x height picture: a red velvet mat, a thin gold lip, a deep dark
        // molding, a broad gold outer molding with rosettes at the corners, and a gold crest with a medallion on top.
        private static void CreateGildedFrame(Transform parent, float width, float height, Material molding, Material gold, Material velvet)
        {
            Mesh round = CreateRoundedBoxMesh(Folder + "/Bulb.asset", 2, 0.5f);
            const float mat = 0.08f, lip = 0.03f, main = 0.12f, outer = 0.06f;
            CreateCube("Velvet Mat", parent, new Vector3(0f, 0f, 0.008f), new Vector3(width + mat * 2f, height + mat * 2f, 0.01f), velvet, false);
            float w = width + mat * 2f, h = height + mat * 2f;
            CreateFrameRing(parent, "Gold Lip", w, h, lip, 0.04f, -0.015f, gold);
            w += lip * 2f; h += lip * 2f;
            CreateFrameRing(parent, "Molding", w, h, main, 0.06f, -0.025f, molding);
            w += main * 2f; h += main * 2f;
            CreateFrameRing(parent, "Gold Molding", w, h, outer, 0.09f, -0.04f, gold);

            float cornerX = (w + outer) / 2f, cornerY = (h + outer) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var rosette = CreateMeshObject("Rosette", parent, round, gold).transform;
                rosette.SetLocalPositionAndRotation(new Vector3(i % 2 == 0 ? -cornerX : cornerX, i < 2 ? -cornerY : cornerY, -0.08f), Quaternion.identity);
                rosette.localScale = Vector3.one * 0.15f;
            }

            // A half disc rising behind the top of the frame (its lower half hides behind the moldings).
            float top = h / 2f + outer;
            CreateCylinder("Crest", parent, new Vector3(0f, top, 0.01f), new Vector3(0.6f, 0.01f, 0.6f), new Vector3(90f, 0f, 0f), gold, false);
            var medallion = CreateMeshObject("Medallion", parent, CreateRoundedBoxMesh(Folder + "/Jewel.asset", 5, 0.5f), velvet).transform;
            medallion.SetLocalPositionAndRotation(new Vector3(0f, top + 0.12f, -0.01f), Quaternion.identity);
            medallion.localScale = new Vector3(0.14f, 0.14f, 0.04f);
            CreateCylinder("Medallion Rim", parent, new Vector3(0f, top + 0.12f, 0f), new Vector3(0.19f, 0.01f, 0.19f), new Vector3(90f, 0f, 0f), gold, false);
        }

        // Four bars of `bar` width around an inner width x height opening, `depth` deep, centered at local z.
        private static void CreateFrameRing(Transform parent, string name, float width, float height, float bar, float depth, float z, Material material)
        {
            CreateCube(name + " Top", parent, new Vector3(0f, (height + bar) / 2f, z), new Vector3(width + bar * 2f, bar, depth), material, false);
            CreateCube(name + " Bottom", parent, new Vector3(0f, -(height + bar) / 2f, z), new Vector3(width + bar * 2f, bar, depth), material, false);
            CreateCube(name + " Left", parent, new Vector3(-(width + bar) / 2f, 0f, z), new Vector3(bar, height, depth), material, false);
            CreateCube(name + " Right", parent, new Vector3((width + bar) / 2f, 0f, z), new Vector3(bar, height, depth), material, false);
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
