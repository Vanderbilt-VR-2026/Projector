using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // The lobby: a graybox waiting room where joined players see the code and player list until the host starts.
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";
        private const string GeneratedFolder = "Assets/Generated/Graybox";

        [MenuItem("Projector/Build Lobby Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Lobby";

            Material walls = CreateMaterial(GeneratedFolder, "GrayWall", new Color(0.62f, 0.63f, 0.65f), 0.05f, 0f);
            Material floor = CreateMaterial(GeneratedFolder, "GrayFloor", new Color(0.30f, 0.31f, 0.33f), 0.1f, 0f);
            Material stand = CreateMaterial(GeneratedFolder, "DarkGray", new Color(0.12f, 0.13f, 0.14f), 0.3f, 0f);

            CreateRoomShell("Lobby Room", Vector3.zero, new Vector3(10f, 3.5f, 8f), walls, floor);
            CreateSpawnPads();
            CreateDirectionalLight("Directional Light", Color.white, 1.2f, new Vector3(50f, -30f, 0f));
            SetFlatAmbient(new Color(0.55f, 0.56f, 0.6f));

            GameObject rig = SceneBuildUtility.CreateXrRig("XR Origin - Quest 3 Hands and Controllers", new Vector3(0f, 0f, -2.5f), Quaternion.identity, BackgroundColor);
            CreateLobbyBoard(stand, rig.GetComponentInChildren<Camera>(true));
            EnsureEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Lobby scene created at " + ScenePath);
        }

        // One colored pad per player slot, where players will stand once avatars are networked.
        private static void CreateSpawnPads()
        {
            GameObject root = new GameObject("Spawn Pads");
            for (int i = 0; i < PlayerColors.Length; i++)
            {
                Material pad = CreateMaterial(GeneratedFolder, "Player" + (i + 1), PlayerColors[i], 0.2f, 0f);
                float x = (i - (PlayerColors.Length - 1) / 2f) * 1.5f;
                CreateCylinder("Pad " + (i + 1), root.transform, new Vector3(x, 0.01f, -1.5f), new Vector3(0.9f, 0.01f, 0.9f), Vector3.zero, pad, false);
            }
        }

        private static void CreateLobbyBoard(Material standMaterial, Camera camera)
        {
            GameObject root = new GameObject("Lobby Board");
            root.transform.position = new Vector3(0f, 0f, 0f);
            CreateCube("Stand", root.transform, new Vector3(0f, 0.5f, 0.05f), new Vector3(0.12f, 1f, 0.12f), standMaterial, true);

            LobbyMenu controller = root.AddComponent<LobbyMenu>();
            Canvas canvas = CreateWorldCanvas("Lobby Canvas", root.transform, new Vector2(1400f, 900f), camera);
            canvas.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var panel = CreatePanel("Panel", canvas.transform);

            var title = CreateText("LOBBY", panel, 72, Color.white);
            SetAnchors(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.98f));

            var codeLabel = CreateText("LOBBY CODE", panel, 40, Color.white);
            SetAnchors(codeLabel.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.45f, 0.85f));
            var codeText = CreateText("------", panel, 96, AccentColor, "Code");
            SetAnchors(codeText.rectTransform, new Vector2(0.05f, 0.58f), new Vector2(0.45f, 0.78f));

            var playersLabel = CreateText("PLAYERS", panel, 40, Color.white);
            SetAnchors(playersLabel.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.95f, 0.85f));
            var playersText = CreateText("", panel, 48, Color.white, "Players");
            playersText.alignment = TextAnchor.UpperLeft;
            playersText.lineSpacing = 1.2f;
            SetAnchors(playersText.rectTransform, new Vector2(0.55f, 0.3f), new Vector2(0.95f, 0.76f));

            var statusText = CreateText("", panel, 36, AccentColor, "Status");
            SetAnchors(statusText.rectTransform, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.28f));

            var start = CreateButton("START GAME", panel, 44);
            SetAnchors(start.GetComponent<RectTransform>(), new Vector2(0.55f, 0.04f), new Vector2(0.92f, 0.17f));
            UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);

            var leave = CreateButton("LEAVE", panel, 44);
            SetAnchors(leave.GetComponent<RectTransform>(), new Vector2(0.08f, 0.04f), new Vector2(0.45f, 0.17f));
            UnityEventTools.AddPersistentListener(leave.onClick, controller.LeaveLobby);

            SetReference(controller, "codeText", codeText);
            SetReference(controller, "playersText", playersText);
            SetReference(controller, "statusText", statusText);
            SetReference(controller, "startButton", start);
        }
    }
}
