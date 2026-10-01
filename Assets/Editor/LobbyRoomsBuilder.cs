using System.Collections.Generic;
using System.IO;
using ProjectorGame.Lobby;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Projector > Lobby Rooms
/// Generates the graybox pre-game rooms (Room1 landing room + Lobby waiting room) and the
/// graybox item mock prefabs that players pick up and cast shadows with.
/// Everything is rebuilt from code so it can be tweaked and regenerated without merge pain.
/// </summary>
public static class LobbyRoomsBuilder
{
    const string GeneratedRoot = "Assets/Generated/LobbyRooms";
    const string ItemRoot = "Assets/Prefabs/ItemMocks";
    const string Room1Path = "Assets/Scenes/Room1.unity";
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    static Font s_Font;

    // ================================================================= menu entries

    [MenuItem("Projector/Lobby Rooms/Build Everything")]
    public static void BuildEverything()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildItemMocks();
        BuildRoom1Scene();
        BuildLobbyScene();
        EditorSceneManager.OpenScene(LobbyPath);
        Debug.Log("[Lobby] Built item mocks, Room1 and Lobby.");
    }

    [MenuItem("Projector/Lobby Rooms/Build Item Mocks")]
    public static void BuildItemMocksMenu()
    {
        BuildItemMocks();
        Debug.Log("[Lobby] Item mock prefabs written to " + ItemRoot);
    }

    [MenuItem("Projector/Lobby Rooms/Build Room1 Scene")]
    public static void BuildRoom1Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (LoadItemPrefabs().Count == 0)
            BuildItemMocks();
        BuildRoom1Scene();
    }

    [MenuItem("Projector/Lobby Rooms/Build Lobby Scene")]
    public static void BuildLobbyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (LoadItemPrefabs().Count == 0)
            BuildItemMocks();
        BuildLobbyScene();
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

    // ================================================================= Room1 (first load / menus)

    static void BuildRoom1Scene()
    {
        EnsureFolders();
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var items = LoadItemPrefabs();

        const float W = 8f, H = 3.5f, D = 8f;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(new Color(0.72f, 0.72f, 0.76f), Quaternion.Euler(20f, 0f, 0f), 1.1f);

        var room = BuildShell("Room1 Shell", W, H, D, "Room1");

        // Ceiling projector (the game's namesake) aimed at the front wall
        var projector = BuildProjectorProp(room, new Vector3(0f, H - 0.45f, -2.6f), Quaternion.identity, true);
        projector.name = "Ceiling Projector";

        // Menu screen on the front wall: menus from the landing/host/join flow show up here
        var dark = Mat("Graybox_Dark", new Color(0.28f, 0.29f, 0.32f), 0.4f);
        var screenMat = Mat("ProjectorScreen", new Color(0.95f, 0.95f, 0.93f), 0.1f, 0f, null, null, new Color(0.12f, 0.12f, 0.12f));
        Box("Menu Screen Frame", room, new Vector3(0f, 1.65f, D / 2f - 0.07f), new Vector3(2.5f, 1.55f, 0.04f), dark, true, false);
        Box("Menu Screen", room, new Vector3(0f, 1.65f, D / 2f - 0.095f), new Vector3(2.3f, 1.35f, 0.01f), screenMat, false, false);
        var anchor = new GameObject("Menu Anchor (menus go here)").transform;
        anchor.SetParent(room, false);
        anchor.localPosition = new Vector3(0f, 1.65f, D / 2f - 0.12f);

        var wallCanvas = MakeCanvas("Front Wall Text", room, new Vector3(0f, 2.95f, D / 2f - 0.06f), new Vector2(1200, 200), 0.0025f);
        MakeText(wallCanvas, "Title", "PROJECTOR", 120, Vector2.zero, new Vector2(1200, 200), new Color(0.15f, 0.16f, 0.2f));
        var screenCanvas = MakeCanvas("Menu Placeholder Text", room, new Vector3(0f, 1.65f, D / 2f - 0.105f), new Vector2(1000, 560), 0.0022f);
        MakeText(screenCanvas, "Placeholder", "Main menu loads here\n(Host / Join with code / Join random)", 44, Vector2.zero, new Vector2(1000, 560), new Color(0.45f, 0.46f, 0.5f));

        // Display shelves on the side walls with the item mocks
        var shelfMat = Mat("Graybox_Mid", new Color(0.55f, 0.56f, 0.6f), 0.3f);
        var leftShelf = new GameObject("Item Shelf Left").transform;
        leftShelf.SetParent(room, false);
        leftShelf.localPosition = new Vector3(-W / 2f + 0.3f, 0f, 0.5f);
        BuildShelf(leftShelf, shelfMat, items, new[] { "Plank", "Block", "L-Block", "Stairs", "Ramp" }, Quaternion.Euler(0f, 90f, 0f));
        var rightShelf = new GameObject("Item Shelf Right").transform;
        rightShelf.SetParent(room, false);
        rightShelf.localPosition = new Vector3(W / 2f - 0.3f, 0f, 0.5f);
        BuildShelf(rightShelf, shelfMat, items, new[] { "Arch", "Trampoline", "Spike Strip", "Buzzsaw", "Coin" }, Quaternion.Euler(0f, -90f, 0f));

        var shelfSignL = MakeCanvas("Shelf Sign Left", room, new Vector3(-W / 2f + 0.06f, 2.15f, 0.5f), new Vector2(900, 120), 0.002f);
        shelfSignL.localRotation = Quaternion.Euler(0f, -90f, 0f);
        MakeText(shelfSignL, "Text", "PLATFORMS", 80, Vector2.zero, new Vector2(900, 120), new Color(0.2f, 0.22f, 0.28f));
        var shelfSignR = MakeCanvas("Shelf Sign Right", room, new Vector3(W / 2f - 0.06f, 2.15f, 0.5f), new Vector2(900, 120), 0.002f);
        shelfSignR.localRotation = Quaternion.Euler(0f, 90f, 0f);
        MakeText(shelfSignR, "Text", "MOVERS + HAZARDS + COINS", 70, Vector2.zero, new Vector2(900, 120), new Color(0.2f, 0.22f, 0.28f));

        // Spawn marker
        SpawnRing(room, new Vector3(0f, 0f, -1.5f), new Color(0.2f, 0.75f, 1f), "Spawn Ring");

        // Dev shortcut into the lobby (until the real menu flow is wired)
        var console = BuildConsole(room, new Vector3(1.0f, 0f, -0.5f), "Lobby Console");
        var loader = console.gameObject.AddComponent<SceneLoader>();
        loader.sceneName = "Lobby";
        var btn = MakeButton(console, new Vector3(0f, 1.18f, -0.15f), new Color(0.2f, 0.85f, 0.5f));
        UnityEventTools.AddPersistentListener(btn.onPressed, new UnityAction(loader.Load));
        var cc = MakeCanvas("Console Text", console, new Vector3(0f, 1.2f, -0.132f), new Vector2(420, 380), 0.001f);
        MakeText(cc, "Label", "ENTER LOBBY", 40, new Vector2(0, 120), new Vector2(420, 60), Color.white);
        MakeText(cc, "Hint", "dev shortcut - poke or point + grip", 22, new Vector2(0, -120), new Vector2(420, 40), new Color(0.75f, 0.75f, 0.8f));

        AddRigAndTeleport(new Vector3(0f, 0f, -1.5f), Quaternion.identity, room.Find("Floor").gameObject);

        EditorSceneManager.SaveScene(scene, Room1Path);
        Debug.Log("[Lobby] Room1 saved to " + Room1Path);
    }

    // ================================================================= Lobby (waiting room)

    static void BuildLobbyScene()
    {
        EnsureFolders();
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var items = LoadItemPrefabs();

        const float W = 10f, H = 4f, D = 12f;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // The key light doubles as the projector beam: it shines toward +X onto the shadow screen.
        SetupLighting(new Color(0.78f, 0.78f, 0.82f), Quaternion.Euler(12f, 90f, 0f), 1.1f);

        var room = BuildShell("Lobby Shell", W, H, D, "Lobby", 0.62f);
        var dark = Mat("Graybox_Dark", new Color(0.28f, 0.29f, 0.32f), 0.4f);
        var mid = Mat("Graybox_Mid", new Color(0.55f, 0.56f, 0.6f), 0.3f);
        var screenMat = Mat("ProjectorScreen", new Color(0.95f, 0.95f, 0.93f), 0.1f, 0f, null, null, new Color(0.12f, 0.12f, 0.12f));

        // ---------------- lobby board on the front wall
        var boardGo = new GameObject("Lobby Board");
        boardGo.transform.SetParent(room, false);
        boardGo.transform.localPosition = new Vector3(0f, 2.3f, D / 2f - 0.06f);
        var board = boardGo.AddComponent<LobbyBoard>();
        Box("Backing", boardGo.transform, new Vector3(0f, 0f, 0.0f), new Vector3(4.4f, 2.1f, 0.05f), dark, true, false);
        var bc = MakeCanvas("Board Canvas", boardGo.transform, new Vector3(0f, 0f, -0.03f), new Vector2(1050, 500), 0.004f);
        MakeImage(bc, "Background", Vector2.zero, new Vector2(1050, 500), new Color(0.1f, 0.11f, 0.14f));
        MakeImage(bc, "Header Bar", new Vector2(0, 205), new Vector2(1050, 90), new Color(0.16f, 0.17f, 0.22f));
        MakeText(bc, "Title", "LOBBY", 64, new Vector2(-330, 205), new Vector2(380, 90), Color.white);
        board.codeText = MakeText(bc, "Code", "CODE  ------", 52, new Vector2(270, 205), new Vector2(500, 90), new Color(1f, 0.85f, 0.3f));
        board.slotTexts = new Text[LobbyBoard.MaxPlayers];
        board.slotDots = new Image[LobbyBoard.MaxPlayers];
        for (int i = 0; i < LobbyBoard.MaxPlayers; i++)
        {
            float x = (i % 2 == 0) ? -255f : 255f;
            float y = (i < 2) ? 80f : -35f;
            MakeImage(bc, "Slot " + (i + 1), new Vector2(x, y), new Vector2(490, 95), new Color(0.15f, 0.16f, 0.2f));
            board.slotDots[i] = MakeImage(bc, "Dot " + (i + 1), new Vector2(x - 195, y), new Vector2(50, 50), board.emptySlotColor);
            MakeText(bc, "Num " + (i + 1), "P" + (i + 1), 30, new Vector2(x - 195, y), new Vector2(60, 50), new Color(0.05f, 0.05f, 0.08f));
            var t = MakeText(bc, "Name " + (i + 1), "open slot", 42, new Vector2(x + 40, y), new Vector2(380, 80), Color.white);
            t.alignment = TextAnchor.MiddleLeft;
            board.slotTexts[i] = t;
        }
        board.statusText = MakeText(bc, "Status", "Waiting for players", 50, new Vector2(-120, -170), new Vector2(760, 90), new Color(0.4f, 0.9f, 1f));
        board.countText = MakeText(bc, "Count", "1 / 4 players", 38, new Vector2(370, -170), new Vector2(280, 90), new Color(0.8f, 0.8f, 0.85f));

        // ---------------- player pads
        var pads = new SpawnPad[LobbyBoard.MaxPlayers];
        for (int i = 0; i < LobbyBoard.MaxPlayers; i++)
        {
            var pos = new Vector3(-2.25f + i * 1.5f, 0f, -1.6f);
            var ring = SpawnRing(room, pos, board.playerColors[i], "Player Pad " + (i + 1));
            var pad = ring.gameObject.AddComponent<SpawnPad>();
            pad.playerIndex = i;
            pad.color = board.playerColors[i];
            pad.ringRenderer = ring.Find("Ring").GetComponent<Renderer>();
            var lc = MakeCanvas("Pad Label", ring, new Vector3(0f, 0.02f, -0.62f), new Vector2(400, 160), 0.002f);
            lc.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pad.label = MakeText(lc, "Label", "P" + (i + 1), 64, Vector2.zero, new Vector2(400, 160), board.playerColors[i]);
            pads[i] = pad;
        }
        board.pads = pads;

        // ---------------- host console in front of the board
        var hostConsole = BuildConsole(room, new Vector3(0f, 0f, 3.6f), "Host Console");
        var startBtn = MakeButton(hostConsole, new Vector3(-0.1f, 1.18f, -0.15f), new Color(0.2f, 0.85f, 0.4f));
        UnityEventTools.AddPersistentListener(startBtn.onPressed, new UnityAction(board.StartCountdown));
        var resetBtn = MakeButton(hostConsole, new Vector3(0.1f, 1.18f, -0.15f), new Color(1f, 0.6f, 0.15f));
        UnityEventTools.AddPersistentListener(resetBtn.onPressed, new UnityAction(board.ResetLobby));
        var hc = MakeCanvas("Console Text", hostConsole, new Vector3(0f, 1.2f, -0.132f), new Vector2(420, 380), 0.001f);
        MakeText(hc, "Start Label", "START", 34, new Vector2(-100, 115), new Vector2(200, 50), Color.white);
        MakeText(hc, "Reset Label", "NEW LOBBY", 30, new Vector2(100, 115), new Vector2(200, 50), Color.white);
        MakeText(hc, "Hint", "host controls", 24, new Vector2(0, -120), new Vector2(420, 40), new Color(0.75f, 0.75f, 0.8f));

        // ---------------- game room door (placeholder, opens when the game scene exists)
        var door = new GameObject("Game Room Door").transform;
        door.SetParent(room, false);
        door.localPosition = new Vector3(3.6f, 0f, D / 2f - 0.06f);
        Box("Frame Top", door, new Vector3(0f, 2.3f, 0f), new Vector3(1.5f, 0.15f, 0.12f), dark, true, false);
        Box("Frame L", door, new Vector3(-0.7f, 1.1f, 0f), new Vector3(0.12f, 2.3f, 0.12f), dark, true, false);
        Box("Frame R", door, new Vector3(0.7f, 1.1f, 0f), new Vector3(0.12f, 2.3f, 0.12f), dark, true, false);
        Box("Door Panel", door, new Vector3(0f, 1.1f, 0.02f), new Vector3(1.3f, 2.2f, 0.04f), mid, true, false);
        var dc = MakeCanvas("Door Sign", door, new Vector3(0f, 2.65f, -0.04f), new Vector2(600, 160), 0.0025f);
        MakeText(dc, "Text", "GAME ROOM", 70, new Vector2(0, 25), new Vector2(600, 90), new Color(0.15f, 0.16f, 0.2f));
        MakeText(dc, "Sub", "opens when the host starts", 34, new Vector2(0, -45), new Vector2(600, 60), new Color(0.35f, 0.36f, 0.42f));

        // ---------------- item sandbox table along the left wall
        var sandbox = new GameObject("Item Sandbox").transform;
        sandbox.SetParent(room, false);
        sandbox.localPosition = new Vector3(-W / 2f + 0.6f, 0f, 0.6f);
        BuildTable(sandbox, new Vector3(0.8f, 0.8f, 4.2f), dark, mid);
        int n = 0;
        foreach (var spec in k_Items)
        {
            if (!items.TryGetValue(spec.name, out var prefab)) continue;
            float z = -1.85f + n * 0.41f;
            Place(prefab, sandbox, new Vector3(0f, 0.83f, z), Quaternion.Euler(0f, 90f, 0f));
            n++;
        }
        var sc = MakeCanvas("Sandbox Sign", room, new Vector3(-W / 2f + 0.06f, 2.0f, 0.6f), new Vector2(1100, 220), 0.0025f);
        sc.localRotation = Quaternion.Euler(0f, -90f, 0f);
        MakeText(sc, "Title", "ITEM SANDBOX", 90, new Vector2(0, 40), new Vector2(1100, 120), new Color(0.15f, 0.16f, 0.2f));
        MakeText(sc, "Sub", "grab a piece while you wait for the others", 44, new Vector2(0, -60), new Vector2(1100, 80), new Color(0.35f, 0.36f, 0.42f));

        // ---------------- shadow preview corner (the core mechanic, playable while waiting)
        var preview = new GameObject("Shadow Preview").transform;
        preview.SetParent(room, false);
        preview.localPosition = new Vector3(0f, 0f, 0.6f);
        Box("Shadow Screen Frame", preview, new Vector3(W / 2f - 0.05f, 1.55f, 0f), new Vector3(0.04f, 2.0f, 3.0f), dark, false, false);
        Box("Shadow Screen", preview, new Vector3(W / 2f - 0.075f, 1.55f, 0f), new Vector3(0.01f, 1.8f, 2.8f), screenMat, false, false);
        var projector = BuildProjectorProp(preview, new Vector3(1.2f, 1.25f, 0f), Quaternion.Euler(0f, 90f, 0f), false);
        Box("Projector Stand", preview, new Vector3(1.2f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.08f), dark, false, false);
        Box("Projector Stand Base", preview, new Vector3(1.2f, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.4f), dark, false, false);
        var beamTable = new GameObject("Beam Table").transform;
        beamTable.SetParent(preview, false);
        beamTable.localPosition = new Vector3(2.2f, 0f, -1.1f);
        BuildTable(beamTable, new Vector3(0.6f, 0.9f, 0.6f), dark, mid);
        if (items.TryGetValue("Stairs", out var stairs)) Place(stairs, beamTable, new Vector3(0f, 0.93f, -0.15f), Quaternion.identity);
        if (items.TryGetValue("Arch", out var arch)) Place(arch, beamTable, new Vector3(0f, 0.93f, 0.15f), Quaternion.identity);
        var pc = MakeCanvas("Preview Sign", room, new Vector3(W / 2f - 0.06f, 2.85f, 0.6f), new Vector2(1100, 200), 0.0025f);
        pc.localRotation = Quaternion.Euler(0f, 90f, 0f);
        MakeText(pc, "Title", "SHADOW PREVIEW", 84, new Vector2(0, 35), new Vector2(1100, 110), new Color(0.15f, 0.16f, 0.2f));
        MakeText(pc, "Sub", "hold a piece in the beam - its shadow is your platform", 40, new Vector2(0, -55), new Vector2(1100, 70), new Color(0.35f, 0.36f, 0.42f));

        // ---------------- dev shortcut back to Room1
        var back = BuildConsole(room, new Vector3(-1.4f, 0f, -3.6f), "Back Console");
        var backLoader = back.gameObject.AddComponent<SceneLoader>();
        backLoader.sceneName = "Room1";
        var backBtn = MakeButton(back, new Vector3(0f, 1.18f, -0.15f), new Color(0.3f, 0.6f, 1f));
        UnityEventTools.AddPersistentListener(backBtn.onPressed, new UnityAction(backLoader.Load));
        var bcc = MakeCanvas("Console Text", back, new Vector3(0f, 1.2f, -0.132f), new Vector2(420, 380), 0.001f);
        MakeText(bcc, "Label", "BACK TO ROOM 1", 34, new Vector2(0, 120), new Vector2(420, 60), Color.white);
        MakeText(bcc, "Hint", "dev shortcut", 22, new Vector2(0, -120), new Vector2(420, 40), new Color(0.75f, 0.75f, 0.8f));

        AddRigAndTeleport(new Vector3(0f, 0f, -4.4f), Quaternion.identity, room.Find("Floor").gameObject);

        EditorSceneManager.SaveScene(scene, LobbyPath);
        Debug.Log("[Lobby] Lobby saved to " + LobbyPath);
    }

    // ================================================================= room pieces

    static void SetupLighting(Color ambient, Quaternion keyRotation, float keyIntensity)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambient;
        RenderSettings.ambientEquatorColor = ambient * 0.85f;
        RenderSettings.ambientGroundColor = ambient * 0.55f;
        RenderSettings.fog = false;

        var key = new GameObject("Projector Key Light").AddComponent<Light>();
        key.type = LightType.Directional;
        key.transform.rotation = keyRotation;
        key.intensity = keyIntensity;
        key.color = new Color(1f, 0.97f, 0.9f);
        key.shadows = LightShadows.Hard;
        key.shadowStrength = 0.85f;
    }

    /// <summary>Floor, ceiling and four walls with a 1 m graybox grid. Walls/ceiling don't cast shadows so the key light reaches inside.</summary>
    static Transform BuildShell(string name, float w, float h, float d, string prefix, float wallTone = 0.86f)
    {
        var grid = GridTexture();
        var root = new GameObject(name).transform;
        var floor = Box("Floor", root, new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), GridMat(prefix + "_Floor", grid, new Color(0.62f, 0.63f, 0.66f), new Vector2(w, d)), true, false);
        Box("Ceiling", root, new Vector3(0f, h + 0.05f, 0f), new Vector3(w, 0.1f, d), GridMat(prefix + "_Ceiling", grid, new Color(0.8f, 0.8f, 0.82f), new Vector2(w, d)), false, false);
        var longWall = GridMat(prefix + "_WallLong", grid, new Color(wallTone, wallTone, wallTone + 0.02f), new Vector2(d, h));
        var shortWall = GridMat(prefix + "_WallShort", grid, new Color(wallTone, wallTone, wallTone + 0.02f), new Vector2(w, h));
        Box("Wall Front", root, new Vector3(0f, h / 2f, d / 2f + 0.05f), new Vector3(w, h, 0.1f), shortWall, true, false);
        Box("Wall Back", root, new Vector3(0f, h / 2f, -d / 2f - 0.05f), new Vector3(w, h, 0.1f), shortWall, true, false);
        Box("Wall Left", root, new Vector3(-w / 2f - 0.05f, h / 2f, 0f), new Vector3(0.1f, h, d), longWall, true, false);
        Box("Wall Right", root, new Vector3(w / 2f + 0.05f, h / 2f, 0f), new Vector3(0.1f, h, d), longWall, true, false);

        var trim = Mat("Trim", new Color(0.35f, 0.36f, 0.4f), 0.3f);
        Box("Baseboard Front", root, new Vector3(0f, 0.06f, d / 2f - 0.01f), new Vector3(w, 0.12f, 0.02f), trim, false, false);
        Box("Baseboard Back", root, new Vector3(0f, 0.06f, -d / 2f + 0.01f), new Vector3(w, 0.12f, 0.02f), trim, false, false);
        Box("Baseboard Left", root, new Vector3(-w / 2f + 0.01f, 0.06f, 0f), new Vector3(0.02f, 0.12f, d), trim, false, false);
        Box("Baseboard Right", root, new Vector3(w / 2f - 0.01f, 0.06f, 0f), new Vector3(0.02f, 0.12f, d), trim, false, false);

        var lightMat = Mat("CeilingLight", Color.white, 0.2f, 0f, null, null, new Color(1f, 0.98f, 0.92f) * 1.5f);
        for (int i = -1; i <= 1; i += 2)
            Box("Ceiling Light Strip", root, new Vector3(i * w / 4f, h - 0.02f, 0f), new Vector3(0.25f, 0.03f, d * 0.7f), lightMat, false, false);
        return root;
    }

    static Transform BuildProjectorProp(Transform parent, Vector3 localPos, Quaternion rot, bool ceilingMount)
    {
        var body = Mat("ProjectorBody", new Color(0.18f, 0.18f, 0.2f), 0.6f);
        var lens = Mat("ProjectorLens", new Color(0.6f, 0.8f, 1f), 0.95f, 0f, null, null, new Color(0.9f, 0.95f, 1f) * 2f);
        var p = new GameObject("Projector").transform;
        p.SetParent(parent, false);
        p.localPosition = localPos;
        p.localRotation = rot;
        Box("Body", p, Vector3.zero, new Vector3(0.36f, 0.16f, 0.42f), body, false, false);
        var l = Cyl("Lens", p, new Vector3(0.08f, 0f, 0.22f), new Vector3(0.1f, 0.03f, 0.1f), lens, false);
        l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        l.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        if (ceilingMount)
            Box("Mount", p, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.45f, 0.05f), body, false, false);
        return p;
    }

    static void BuildShelf(Transform shelf, Material mat, Dictionary<string, GameObject> items, string[] names, Quaternion itemRot)
    {
        Box("Upright A", shelf, new Vector3(0f, 0.9f, -1.3f), new Vector3(0.4f, 1.8f, 0.04f), mat, true, false);
        Box("Upright B", shelf, new Vector3(0f, 0.9f, 1.3f), new Vector3(0.4f, 1.8f, 0.04f), mat, true, false);
        Box("Shelf Low", shelf, new Vector3(0f, 0.75f, 0f), new Vector3(0.4f, 0.03f, 2.6f), mat, true, false);
        Box("Shelf High", shelf, new Vector3(0f, 1.3f, 0f), new Vector3(0.4f, 0.03f, 2.6f), mat, true, false);
        for (int i = 0; i < names.Length; i++)
        {
            if (!items.TryGetValue(names[i], out var prefab)) continue;
            bool high = i % 2 == 1;
            float z = -1.0f + i * 0.5f;
            Place(prefab, shelf, new Vector3(0f, high ? 1.32f : 0.77f, z), itemRot);
        }
    }

    static void BuildTable(Transform parent, Vector3 size, Material top, Material legs)
    {
        Box("Table Top", parent, new Vector3(0f, size.y - 0.025f, 0f), new Vector3(size.x, 0.05f, size.z), top, true, false);
        float lx = size.x / 2f - 0.05f, lz = size.z / 2f - 0.05f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box("Leg", parent, new Vector3(sx * lx, (size.y - 0.05f) / 2f, sz * lz), new Vector3(0.05f, size.y - 0.05f, 0.05f), legs, true, false);
    }

    static Transform BuildConsole(Transform parent, Vector3 localPos, string name)
    {
        var dark = Mat("Graybox_Dark", new Color(0.28f, 0.29f, 0.32f), 0.4f);
        var c = new GameObject(name).transform;
        c.SetParent(parent, false);
        c.localPosition = localPos;
        Box("Pedestal", c, new Vector3(0f, 0.475f, 0f), new Vector3(0.45f, 0.95f, 0.25f), dark, true, false);
        Box("Panel", c, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 0.4f, 0.25f), dark, true, false);
        return c;
    }

    static PokeButton MakeButton(Transform parent, Vector3 localPos, Color color)
    {
        var steel = Mat("Accent_Steel", new Color(0.75f, 0.78f, 0.82f), 0.85f, 0.9f);
        var capMat = Mat("ButtonCap", Color.white, 0.6f, 0f, null, null, Color.black);

        var root = new GameObject("Button");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        Box("Bezel", root.transform, new Vector3(0f, 0f, 0.012f), new Vector3(0.12f, 0.12f, 0.02f), steel, false, false);
        var cap = Box("Cap", root.transform, new Vector3(0f, 0f, -0.01f), new Vector3(0.09f, 0.09f, 0.03f), capMat, false, false);

        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(0.1f, 0.1f, 0.05f);
        box.center = new Vector3(0f, 0f, -0.01f);

        var interactable = root.AddComponent<XRSimpleInteractable>();
        var poke = root.AddComponent<XRPokeFilter>();
        poke.pokeCollider = box;
        var so = new SerializedObject(poke);
        var prop = so.FindProperty("m_Interactable");
        if (prop != null)
        {
            prop.objectReferenceValue = interactable;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var btn = root.AddComponent<PokeButton>();
        btn.cap = cap.transform;
        btn.capRenderer = cap.GetComponent<Renderer>();
        btn.idleColor = color;
        btn.hoverColor = Color.Lerp(color, Color.white, 0.45f);
        return btn;
    }

    static Transform SpawnRing(Transform parent, Vector3 localPos, Color color, string name)
    {
        string key = ColorUtility.ToHtmlStringRGB(color);
        var ringMat = Mat("PadRing_" + key, color, 0.5f, 0f, null, null, color * 0.6f);
        var inner = Mat("Graybox_Mid", new Color(0.55f, 0.56f, 0.6f), 0.3f);
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        var ring = Cyl("Ring", root, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.005f, 0.9f), ringMat, false);
        Cyl("Inner", root, new Vector3(0f, 0.011f, 0f), new Vector3(0.72f, 0.005f, 0.72f), inner, false);
        return root;
    }

    static void AddRigAndTeleport(Vector3 pos, Quaternion rot, GameObject floor)
    {
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
        if (rigPrefab != null)
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            Debug.LogWarning("[Lobby] XR Origin prefab not found at " + XROriginPath);
        }

        int mask = InteractionLayerMask.GetMask("Teleport");
        if (mask != 0 && floor != null)
            floor.AddComponent<TeleportationArea>().interactionLayers = mask;
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        return go;
    }

    // ================================================================= primitive helpers

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material m, bool collider, bool castShadows)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (!collider)
            Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

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

    // ================================================================= UI helpers

    static RectTransform MakeCanvas(string name, Transform parent, Vector3 localPos, Vector2 size, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localPosition = localPos;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * scale;
        return rt;
    }

    static Text MakeText(RectTransform canvas, string name, string text, int size, Vector2 pos, Vector2 box, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<Text>();
        t.font = s_Font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Image MakeImage(RectTransform canvas, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ================================================================= assets

    static void EnsureFolders()
    {
        Directory.CreateDirectory(GeneratedRoot);
        Directory.CreateDirectory(GeneratedRoot + "/Meshes");
        Directory.CreateDirectory(ItemRoot);
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
    }

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

    static Material GridMat(string name, Texture grid, Color tint, Vector2 tiling)
    {
        return Mat(name, tint, 0.2f, 0f, grid, tiling);
    }

    static Texture2D GridTexture()
    {
        string path = $"{GeneratedRoot}/GrayboxGrid.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool major = x < 3 || y < 3;
                bool minor = (x % 64 < 1) || (y % 64 < 1);
                float v = major ? 0.55f : (minor ? 0.82f : 1f);
                px[y * size + x] = new Color(v, v, v, 1f);
            }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
