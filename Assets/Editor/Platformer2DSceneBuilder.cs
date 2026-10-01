using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // Graybox of the 2D race: a flat wall behind a play plane at z = 0, start and end platforms,
    // avatar dummies, and the results scoreboard beside the viewer. Shadow platforms will be generated
    // under "Shadow Platforms" between the start and the finish.
    public static class Platformer2DSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Platformer2D.unity";
        private const string GeneratedFolder = "Assets/Generated/Graybox";

        private const float LevelHalfWidth = 10f;
        private const float PlatformTop = 2f;
        private static readonly Vector3 PlatformSize = new Vector3(3f, 1f, 1.5f);

        [MenuItem("Projector/Build Platformer 2D Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Platformer 2D";

            Material ground = CreateMaterial(GeneratedFolder, "GrayFloor", new Color(0.30f, 0.31f, 0.33f), 0.1f, 0f);
            Material wall = CreateMaterial(GeneratedFolder, "ProjectionWall", new Color(0.82f, 0.82f, 0.8f), 0.05f, 0f);
            Material start = CreateMaterial(GeneratedFolder, "StartPlatform", new Color(0.3f, 0.62f, 0.38f), 0.1f, 0f);
            Material finish = CreateMaterial(GeneratedFolder, "FinishPlatform", new Color(0.85f, 0.68f, 0.25f), 0.2f, 0.1f);
            Material dark = CreateMaterial(GeneratedFolder, "DarkGray", new Color(0.12f, 0.13f, 0.14f), 0.3f, 0f);

            GameObject level = new GameObject("Level");
            CreateCube("Ground", level.transform, new Vector3(0f, -0.05f, -5f), new Vector3(26f, 0.1f, 18f), ground, true);
            CreateCube("Flat Wall", level.transform, new Vector3(0f, 4f, 1.2f), new Vector3(LevelHalfWidth * 2f + 2f, 8f, 0.2f), wall, true);
            new GameObject("Shadow Platforms").transform.SetParent(level.transform);

            GameObject startPlatform = CreatePlatform("Start Platform", level.transform, -LevelHalfWidth + PlatformSize.x / 2f, start, "START");
            GameObject endPlatform = CreatePlatform("End Platform", level.transform, LevelHalfWidth - PlatformSize.x / 2f, finish, "FINISH");
            CreateFlag(endPlatform.transform, dark);
            CreateAvatarDummies(startPlatform.transform.position.x);

            CreateDirectionalLight("Directional Light", new Color(1f, 0.96f, 0.9f), 1f, new Vector3(25f, 15f, 0f), LightShadows.Soft);
            SetFlatAmbient(new Color(0.5f, 0.52f, 0.56f));

            // The viewer stands back from the play plane so the whole level fits in view.
            GameObject rig = SceneBuildUtility.CreateXrRig("XR Origin - Quest 3 Hands and Controllers", new Vector3(0f, 0f, -11f), Quaternion.identity, BackgroundColor);
            CreateScoreboard(dark, rig.GetComponentInChildren<Camera>(true));
            EnsureEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Platformer 2D scene created at " + ScenePath);
        }

        private static GameObject CreatePlatform(string name, Transform parent, float x, Material material, string label)
        {
            GameObject platform = CreateCube(name, parent, new Vector3(x, PlatformTop - PlatformSize.y / 2f, 0f), PlatformSize, material, true);

            Canvas canvas = CreateWorldCanvas(name + " Label", parent, new Vector2(600f, 160f), null);
            canvas.transform.localScale = Vector3.one * 0.004f;
            canvas.transform.position = new Vector3(x, PlatformTop + 2.9f, 0f);
            Text text = CreateText(label, canvas.transform, 120, new Color(0.15f, 0.16f, 0.18f));
            SetFullSize(text.rectTransform);
            return platform;
        }

        private static void CreateFlag(Transform endPlatform, Material poleMaterial)
        {
            Material flag = CreateMaterial(GeneratedFolder, "FlagRed", new Color(0.86f, 0.24f, 0.22f), 0.2f, 0f);
            float poleX = endPlatform.position.x + PlatformSize.x / 2f - 0.3f;
            const float poleHeight = 2.4f;
            CreateCylinder("Flag Pole", endPlatform.parent, new Vector3(poleX, PlatformTop + poleHeight / 2f, 0f), new Vector3(0.06f, poleHeight / 2f, 0.06f), Vector3.zero, poleMaterial, false);
            CreateCube("Flag", endPlatform.parent, new Vector3(poleX - 0.42f, PlatformTop + poleHeight - 0.3f, 0f), new Vector3(0.8f, 0.5f, 0.04f), flag, false);
        }

        // Capsule stand-ins for player avatars, one per slot in the player colors, lined up on the start platform.
        private static void CreateAvatarDummies(float startX)
        {
            GameObject root = new GameObject("Avatar Dummies");
            const float height = 0.8f;
            for (int i = 0; i < PlayerColors.Length; i++)
            {
                Material color = CreateMaterial(GeneratedFolder, "Player" + (i + 1), PlayerColors[i], 0.2f, 0f);
                float x = startX + (i - (PlayerColors.Length - 1) / 2f) * 0.6f;
                CreatePrimitive(PrimitiveType.Capsule, "Avatar Dummy " + (i + 1), root.transform,
                    new Vector3(x, PlatformTop + height / 2f, 0f), new Vector3(0.4f, height / 2f, 0.4f), Vector3.zero, color, true);
            }
        }

        private static void CreateScoreboard(Material standMaterial, Camera camera)
        {
            // Beside and in front of the viewer, turned to face them, within ray reach.
            GameObject root = new GameObject("Scoreboard");
            root.transform.position = new Vector3(2.6f, 0f, -9.6f);
            root.transform.rotation = Quaternion.LookRotation(root.transform.position - new Vector3(0f, 0f, -11f));
            CreateCube("Stand", root.transform, new Vector3(0f, 0.45f, 0.05f), new Vector3(0.12f, 0.9f, 0.12f), standMaterial, true);

            Scoreboard scoreboard = root.AddComponent<Scoreboard>();
            Canvas canvas = CreateWorldCanvas("Scoreboard Canvas", root.transform, new Vector2(1200f, 800f), camera);
            canvas.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            canvas.transform.localRotation = Quaternion.identity;

            var panel = CreatePanel("Panel", canvas.transform);
            var title = CreateText("RESULTS", panel, 64, Color.white);
            SetAnchors(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f));
            var winner = CreateText("", panel, 44, AccentColor, "Winner");
            SetAnchors(winner.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.85f));

            string[] headers = { "#", "PLAYER", "POINTS", "TIME" };
            string[] fields = { "rankColumn", "nameColumn", "pointsColumn", "timeColumn" };
            float[] columnEdges = { 0.05f, 0.15f, 0.55f, 0.75f, 0.95f };
            for (int i = 0; i < headers.Length; i++)
            {
                var header = CreateText(headers[i], panel, 34, new Color(0.7f, 0.75f, 0.8f));
                SetAnchors(header.rectTransform, new Vector2(columnEdges[i], 0.65f), new Vector2(columnEdges[i + 1], 0.72f));

                var column = CreateText("", panel, 44, Color.white, headers[i] + " Column");
                column.alignment = TextAnchor.UpperCenter;
                column.lineSpacing = 1.25f;
                SetAnchors(column.rectTransform, new Vector2(columnEdges[i], 0.2f), new Vector2(columnEdges[i + 1], 0.64f));
                SetReference(scoreboard, fields[i], column);
            }

            var back = CreateButton("RETURN TO MENU", panel);
            SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.16f));
            UnityEventTools.AddPersistentListener(back.onClick, scoreboard.ReturnToMenu);

            SetReference(scoreboard, "winnerText", winner);
            scoreboard.ShowMockResults();
        }
    }
}
