using Projector.Editor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Projector.Editor.SceneBuildUtility;

public static class LandingMenuBuilder
{
    const string LandingMenuScenePath = "Assets/Scenes/LandingMenu.unity";
    const string GeneratedFolder = "Assets/Generated/Graybox";
    const string KeypadCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    const int KeypadColumns = 9;

    [MenuItem("Projector/Create Landing Menu Scene")]
    public static void CreateLandingMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Landing Menu";

        // Room 1: a plain graybox room the player stands in while using the menus.
        var walls = CreateMaterial(GeneratedFolder, "GrayWall", new Color(0.62f, 0.63f, 0.65f), 0.05f, 0f);
        var floor = CreateMaterial(GeneratedFolder, "GrayFloor", new Color(0.30f, 0.31f, 0.33f), 0.1f, 0f);
        CreateRoomShell("Room 1", Vector3.zero, new Vector3(8f, 3.5f, 8f), walls, floor);
        CreateDirectionalLight("Directional Light", Color.white, 1.2f, new Vector3(50f, -30f, 0f));
        SetFlatAmbient(new Color(0.55f, 0.56f, 0.6f));

        SceneBuildUtility.CreateXrRig("XR Origin - Quest 3 Hands and Controllers", Vector3.zero, Quaternion.identity, BackgroundColor);
        CreateLandingMenuObjects();

        EditorSceneManager.SaveScene(scene, LandingMenuScenePath);
        AddSceneToBuildSettings(LandingMenuScenePath, first: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Projector/Rebuild Landing Menu Objects")]
    public static void RebuildLandingMenuObjects()
    {
        var controller = Object.FindFirstObjectByType<LandingMenu>();
        if (controller == null)
        {
            CreateLandingMenuObjects();
            return;
        }

        var existingMenu = controller.transform.Find("Landing Menu");
        if (existingMenu != null)
            Undo.DestroyObjectImmediate(existingMenu.gameObject);

        CreateLandingMenuObjects();
    }

    [MenuItem("Projector/Create Landing Menu Objects")]
    public static void CreateLandingMenuObjects()
    {
        var controller = Object.FindFirstObjectByType<LandingMenu>();
        if (controller == null)
        {
            var controllerObject = new GameObject("Landing Menu Controller");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create Landing Menu Controller");
            controller = controllerObject.AddComponent<LandingMenu>();
        }

        var existingMenu = controller.transform.Find("Landing Menu");
        if (existingMenu != null)
        {
            Selection.activeGameObject = existingMenu.gameObject;
            return;
        }

        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var canvas = CreateWorldCanvas("Landing Menu", controller.transform, new Vector2(1200f, 1000f), camera);
        PlaceInFrontOf(canvas.transform, camera, 2f);

        // Status line under both pages, for "Creating lobby..." and errors.
        var statusBar = CreateImage("Status Bar", canvas.transform, PanelColor);
        SetAnchors(statusBar.rectTransform, Vector2.zero, new Vector2(1f, 0.07f));
        var statusText = CreateText("", statusBar.transform, 30, AccentColor, "Status");
        SetFullSize(statusText.rectTransform);

        var mainPanel = CreateMainPanel(canvas.transform, controller);
        Text codeText;
        var joinPanel = CreateJoinPanel(canvas.transform, controller, out codeText);
        joinPanel.gameObject.SetActive(false);

        SetReference(controller, "targetCamera", camera);
        SetReference(controller, "codeText", codeText);
        SetReference(controller, "statusText", statusText);
        SetReference(controller, "menuRoot", canvas.transform);
        SetReference(controller, "mainPanel", mainPanel.gameObject);
        SetReference(controller, "joinPanel", joinPanel.gameObject);
        EnsureEventSystem();
        Selection.activeGameObject = canvas.gameObject;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }

    static RectTransform CreateMainPanel(Transform parent, LandingMenu controller)
    {
        var panel = CreatePanel("Main Panel", parent);
        SetAnchors(panel, new Vector2(0f, 0.08f), Vector2.one);

        var title = CreateText("PROJECTOR", panel, 82, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.9f));

        var host = CreateButton("HOST GAME", panel);
        SetAnchors(host.GetComponent<RectTransform>(), new Vector2(0.28f, 0.46f), new Vector2(0.72f, 0.6f));
        UnityEventTools.AddPersistentListener(host.onClick, controller.HostGame);

        var join = CreateButton("JOIN GAME", panel);
        SetAnchors(join.GetComponent<RectTransform>(), new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.42f));
        UnityEventTools.AddPersistentListener(join.onClick, controller.ShowJoin);

        var quit = CreateButton("QUIT", panel, 28);
        SetAnchors(quit.GetComponent<RectTransform>(), new Vector2(0.4f, 0.08f), new Vector2(0.6f, 0.18f));
        UnityEventTools.AddPersistentListener(quit.onClick, controller.QuitGame);
        return panel;
    }

    static RectTransform CreateJoinPanel(Transform parent, LandingMenu controller, out Text codeText)
    {
        var panel = CreatePanel("Join Panel", parent);
        SetAnchors(panel, new Vector2(0f, 0.08f), Vector2.one);

        var title = CreateText("ENTER LOBBY CODE", panel, 48, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.97f));

        codeText = CreateText("______", panel, 84, AccentColor, "Code");
        SetAnchors(codeText.rectTransform, new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.87f));

        // 36 keys in a 9 x 4 grid: A-Z then 0-9.
        var keypad = new GameObject("Keypad", typeof(RectTransform)).GetComponent<RectTransform>();
        Undo.RegisterCreatedObjectUndo(keypad.gameObject, "Create Keypad");
        keypad.SetParent(panel, false);
        SetAnchors(keypad, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.71f));
        var rows = Mathf.CeilToInt(KeypadCharacters.Length / (float)KeypadColumns);
        const float gap = 0.01f;
        for (var index = 0; index < KeypadCharacters.Length; index++)
        {
            var character = KeypadCharacters[index].ToString();
            var column = index % KeypadColumns;
            var row = index / KeypadColumns;
            var key = CreateButton(character, keypad, 44);
            SetAnchors(key.GetComponent<RectTransform>(),
                new Vector2(column / (float)KeypadColumns + gap, 1f - (row + 1) / (float)rows + gap),
                new Vector2((column + 1) / (float)KeypadColumns - gap, 1f - row / (float)rows - gap));
            UnityEventTools.AddStringPersistentListener(key.onClick, controller.AppendCodeCharacter, character);
        }

        var back = CreateButton("BACK", panel);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.06f, 0.05f), new Vector2(0.32f, 0.17f));
        UnityEventTools.AddPersistentListener(back.onClick, controller.ShowMain);

        var delete = CreateButton("DELETE", panel);
        SetAnchors(delete.GetComponent<RectTransform>(), new Vector2(0.37f, 0.05f), new Vector2(0.63f, 0.17f));
        UnityEventTools.AddPersistentListener(delete.onClick, controller.DeleteCodeCharacter);

        var join = CreateButton("JOIN", panel);
        SetAnchors(join.GetComponent<RectTransform>(), new Vector2(0.68f, 0.05f), new Vector2(0.94f, 0.17f));
        UnityEventTools.AddPersistentListener(join.onClick, controller.JoinGame);
        return panel;
    }
}
