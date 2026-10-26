using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Projector.Editor;

public static class LandingMenuBuilder
{
    const string LandingMenuScenePath = "Assets/Scenes/LandingMenu.unity";

    [MenuItem("Projector/Create Landing Menu Scene")]
    public static void CreateLandingMenuScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LandingMenuScenePath) != null)
        {
            EditorUtility.DisplayDialog("Landing Menu Scene", "LandingMenu.unity already exists.", "OK");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildEnvironment();
        CreateLandingMenuObjects();
        EditorSceneManager.SaveScene(scene, LandingMenuScenePath);
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

            var existingHostMenu = controller.transform.Find("Host Menu");
            if (existingHostMenu != null)
                Undo.DestroyObjectImmediate(existingHostMenu.gameObject);

            var existingJoinMenu = controller.transform.Find("Join Game Menu");
            if (existingJoinMenu != null)
                Undo.DestroyObjectImmediate(existingJoinMenu.gameObject);

            var existingGameFoundMenu = controller.transform.Find("Game Found Menu");
            if (existingGameFoundMenu != null)
                Undo.DestroyObjectImmediate(existingGameFoundMenu.gameObject);

            var existingLobbyMenu = controller.transform.Find("Lobby Menu");
            if (existingLobbyMenu != null)
                Undo.DestroyObjectImmediate(existingLobbyMenu.gameObject);

            var existingRandomGameMenu = controller.transform.Find("Random Game Menu");
            if (existingRandomGameMenu != null)
                Undo.DestroyObjectImmediate(existingRandomGameMenu.gameObject);

            CreateLandingMenuObjects();
        }

    public static void RebuildLandingMenuScene()
    {
        var scene = EditorSceneManager.OpenScene(LandingMenuScenePath, OpenSceneMode.Single);
        BuildEnvironment();
        RebuildLandingMenuObjects();
        EditorSceneManager.SaveScene(scene, LandingMenuScenePath);
    }

    // The same hands-and-controllers rig as the projector room (so players can poke the menus with their hands),
    // standing in the theater backdrop. Replaces any previous rig, floor and light.
    static void BuildEnvironment()
    {
        foreach (var origin in Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(origin.gameObject);
        foreach (var name in new[] { "XR Floor", "Directional Light" })
        {
            var leftover = GameObject.Find(name);
            if (leftover != null)
                Undo.DestroyObjectImmediate(leftover);
        }

        SceneBuildUtility.CreateXrRig("XR Origin - Quest 3 Hands and Controllers", Vector3.zero, Quaternion.identity, SceneBuildUtility.BackgroundColor);
        LandingBackdropBuilder.Build();
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
        var existingJoinMenu = controller.transform.Find("Join Game Menu");
        var existingLobbyMenu = controller.transform.Find("Lobby Menu");
        var existingRandomGameMenu = controller.transform.Find("Random Game Menu");
        if (existingMenu != null && existingJoinMenu != null && existingLobbyMenu != null && existingRandomGameMenu != null)
        {
            Selection.activeGameObject = existingMenu.gameObject;
            return;
        }

        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var landingMenu = CreateCanvas("Landing Menu", controller.transform, camera);
        landingMenu.GetComponent<RectTransform>().sizeDelta = new Vector2(1400f, 1000f);
        var panel = CreateImage("Panel", landingMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(panel.rectTransform);

        var title = CreateText("PROJECTOR", panel.transform, 82, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.92f));

        var button = CreateButton("HOST PRIVATE GAME", panel.transform);
        SetAnchors(button.GetComponent<RectTransform>(), new Vector2(0.32f, 0.57f), new Vector2(0.68f, 0.70f));
        UnityEventTools.AddPersistentListener(button.onClick, controller.HostPrivateGame);

        var publicHostButton = CreateButton("HOST PUBLIC GAME", panel.transform);
        SetAnchors(publicHostButton.GetComponent<RectTransform>(), new Vector2(0.32f, 0.42f), new Vector2(0.68f, 0.55f));
        UnityEventTools.AddPersistentListener(publicHostButton.onClick, controller.HostPublicGame);

        var joinButton = CreateButton("JOIN GAME WITH CODE", panel.transform);
        SetAnchors(joinButton.GetComponent<RectTransform>(), new Vector2(0.32f, 0.27f), new Vector2(0.68f, 0.40f));
        UnityEventTools.AddPersistentListener(joinButton.onClick, controller.JoinGameWithCode);

        var randomButton = CreateButton("JOIN RANDOM GAME", panel.transform);
        SetAnchors(randomButton.GetComponent<RectTransform>(), new Vector2(0.32f, 0.12f), new Vector2(0.68f, 0.25f));
        UnityEventTools.AddPersistentListener(randomButton.onClick, controller.JoinRandomGame);

        // Status line under the buttons: "Creating game...", join errors.
        var statusText = CreateText("", panel.transform, 34, new Color(1f, 0.8f, 0.3f));
        statusText.gameObject.name = "Status";
        SetAnchors(statusText.rectTransform, new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.1f));

        var lobbyMenu = CreateCanvas("Lobby Menu", controller.transform, camera);
        lobbyMenu.gameObject.SetActive(false);
        var lobbyPanel = CreateImage("Panel", lobbyMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(lobbyPanel.rectTransform);

        var lobbyCodeText = CreateText("", lobbyPanel.transform, 64, new Color(1f, 0.8f, 0.3f));
        SetAnchors(lobbyCodeText.rectTransform, new Vector2(0.15f, 0.65f), new Vector2(0.85f, 0.9f));

        var playerListTitle = CreateText("CURRENT PLAYERS", lobbyPanel.transform, 34, Color.white);
        SetAnchors(playerListTitle.rectTransform, new Vector2(0.2f, 0.54f), new Vector2(0.8f, 0.63f));

        var playerListText = CreateText("", lobbyPanel.transform, 34, Color.white);
        SetAnchors(playerListText.rectTransform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.54f));
        playerListText.alignment = TextAnchor.UpperCenter;

        var lobbyMenuController = lobbyMenu.AddComponent<GameFoundMenu>();
        var lobbyBackButton = CreateButton("BACK", lobbyPanel.transform);
        SetAnchors(lobbyBackButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.24f, 0.15f));
        UnityEventTools.AddPersistentListener(lobbyBackButton.onClick, controller.BackToLanding);

        var startGameButton = CreateButton("START GAME", lobbyPanel.transform);
        SetAnchors(startGameButton.GetComponent<RectTransform>(), new Vector2(0.76f, 0.06f), new Vector2(0.94f, 0.15f));
        startGameButton.GetComponentInChildren<Text>().fontSize = 22;
        UnityEventTools.AddPersistentListener(startGameButton.onClick, lobbyMenuController.StartGame);

        var randomGameMenu = CreateCanvas("Random Game Menu", controller.transform, camera);
        randomGameMenu.gameObject.SetActive(false);
        var randomPanel = CreateImage("Panel", randomGameMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(randomPanel.rectTransform);

        var lookingText = CreateText("Looking for game...", randomPanel.transform, 54, Color.white);
        SetAnchors(lookingText.rectTransform, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.58f));

        var randomGameMenuController = randomGameMenu.AddComponent<RandomGameMenu>();
        var randomBackButton = CreateButton("BACK", randomPanel.transform);
        SetAnchors(randomBackButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.24f, 0.15f));
        UnityEventTools.AddPersistentListener(randomBackButton.onClick, controller.BackToLanding);

        // Taller than the other pages so the keyboard's keys can be square.
        var joinMenu = CreateCanvas("Join Game Menu", controller.transform, camera);
        joinMenu.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 1000f);
        joinMenu.gameObject.SetActive(false);
        var joinPanel = CreateImage("Panel", joinMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(joinPanel.rectTransform);

        var joinTitle = CreateText("JOIN GAME", joinPanel.transform, 64, Color.white);
        SetAnchors(joinTitle.rectTransform, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.96f));

        var codeInput = CreateInputField(joinPanel.transform);
        SetAnchors(codeInput.GetComponent<RectTransform>(), new Vector2(0.15f, 0.75f), new Vector2(0.85f, 0.83f));

        var joinMenuController = joinMenu.AddComponent<JoinMenu>();
        CreateVirtualKeyboard(joinPanel.transform, joinMenuController);

        var submitButton = CreateButton("JOIN", joinPanel.transform);
        SetAnchors(submitButton.GetComponent<RectTransform>(), new Vector2(0.3f, 0.1f), new Vector2(0.95f, 0.21f));

        UnityEventTools.AddPersistentListener(submitButton.onClick, joinMenuController.SubmitJoinCode);

        var joinBackButton = CreateButton("BACK", joinPanel.transform);
        SetAnchors(joinBackButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.1f), new Vector2(0.26f, 0.21f));
        UnityEventTools.AddPersistentListener(joinBackButton.onClick, controller.BackToLanding);

        SetPrivateReferences(controller, camera, landingMenu.transform, joinMenu.transform, lobbyMenu.transform, randomGameMenu.transform, lobbyMenuController, lobbyCodeText, playerListText, joinMenuController, codeInput, randomGameMenuController);
        SetReference(controller, "statusText", statusText);
        SetReference(lobbyMenuController, "startButton", startGameButton);
        PlaceMenusAtPlaySpot(controller, landingMenu.transform, joinMenu.transform, lobbyMenu.transform, randomGameMenu.transform);
        EnsureEventSystem();
        Selection.activeGameObject = landingMenu;
        EditorSceneManager.MarkSceneDirty(landingMenu.scene);
    }

    // The menus stand at the backdrop's step 4 instead of following the head (LandingMenu re-applies this at runtime).
    static void PlaceMenusAtPlaySpot(LandingMenu controller, params Transform[] menus)
    {
        var playSpot = GameObject.Find(LandingBackdropBuilder.PlaySpotName);
        SetReference(controller, "menuAnchor", playSpot != null ? playSpot.transform : null);
        if (playSpot == null)
            return;

        // Through the RectTransform: a plain Transform position on a canvas root doesn't survive the scene save.
        foreach (var menu in menus)
        {
            var rect = (RectTransform)menu;
            rect.anchoredPosition3D = rect.parent.InverseTransformPoint(playSpot.transform.position);
            rect.rotation = playSpot.transform.rotation;
        }
    }

    static GameObject CreateCanvas(string objectName, Transform parent, Camera camera)
    {
        var canvasObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Menu Canvas");
        canvasObject.transform.SetParent(parent, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200f, 800f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;
        if (camera != null)
        {
            var menuPosition = camera.transform.position + camera.transform.forward * 2f;
            canvasObject.transform.SetPositionAndRotation(
                menuPosition,
                Quaternion.LookRotation(camera.transform.position - menuPosition, camera.transform.up));
        }

        return canvasObject;
    }

    static void SetReference(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrivateReferences(LandingMenu controller, Camera camera, Transform menuRoot, Transform joinMenuRoot, Transform lobbyMenuRoot, Transform randomGameMenuRoot, GameFoundMenu lobbyMenu, Text lobbyCodeText, Text playerListText, JoinMenu joinMenu, InputField codeInput, RandomGameMenu randomGameMenu)
    {
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("targetCamera").objectReferenceValue = camera;
        serializedController.FindProperty("menuRoot").objectReferenceValue = menuRoot;
        serializedController.FindProperty("joinMenuRoot").objectReferenceValue = joinMenuRoot;
        serializedController.FindProperty("lobbyMenuRoot").objectReferenceValue = lobbyMenuRoot;
        serializedController.FindProperty("randomGameMenuRoot").objectReferenceValue = randomGameMenuRoot;
        serializedController.FindProperty("lobbyMenu").objectReferenceValue = lobbyMenu;
        serializedController.FindProperty("joinMenu").objectReferenceValue = joinMenu;
        serializedController.FindProperty("randomGameMenu").objectReferenceValue = randomGameMenu;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        var serializedLobbyMenu = new SerializedObject(lobbyMenu);
        serializedLobbyMenu.FindProperty("codeText").objectReferenceValue = lobbyCodeText;
        serializedLobbyMenu.FindProperty("playerListText").objectReferenceValue = playerListText;
        serializedLobbyMenu.ApplyModifiedPropertiesWithoutUndo();

        var serializedJoinMenu = new SerializedObject(joinMenu);
        serializedJoinMenu.FindProperty("landingMenu").objectReferenceValue = controller;
        serializedJoinMenu.FindProperty("codeInput").objectReferenceValue = codeInput;
        serializedJoinMenu.ApplyModifiedPropertiesWithoutUndo();

        var serializedRandomGameMenu = new SerializedObject(randomGameMenu);
        serializedRandomGameMenu.FindProperty("landingMenu").objectReferenceValue = controller;
        serializedRandomGameMenu.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureEventSystem()
    {
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var eventSystem = eventSystems.Length > 0 ? eventSystems[0] : null;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("XR Event System");
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create XR Event System");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        for (var index = 1; index < eventSystems.Length; index++)
            Undo.DestroyObjectImmediate(eventSystems[index].gameObject);

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
    }

    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(imageObject, "Create Menu Image");
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    static Text CreateText(string value, Transform parent, int fontSize, Color color)
    {
        var textObject = new GameObject(string.IsNullOrEmpty(value) ? "Code" : value);
        Undo.RegisterCreatedObjectUndo(textObject, "Create Menu Text");
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(string label, Transform parent)
    {
        var buttonObject = new GameObject(label);
        Undo.RegisterCreatedObjectUndo(buttonObject, "Create Menu Button");
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.1f, 0.45f, 0.62f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText(label, buttonObject.transform, 36, Color.white);
        SetFullSize(text.rectTransform);
        return button;
    }

    static InputField CreateInputField(Transform parent)
    {
        var inputObject = CreateImage("Join Code Input", parent, Color.white).gameObject;
        var inputField = inputObject.AddComponent<InputField>();
        inputField.characterLimit = 6;
        inputField.characterValidation = InputField.CharacterValidation.Alphanumeric;

        var text = CreateText("", inputObject.transform, 42, Color.black);
        SetFullSize(text.rectTransform);
        text.alignment = TextAnchor.MiddleCenter;
        inputField.textComponent = text;
        var placeholder = CreateText("ENTER CODE", inputObject.transform, 34, new Color(0.35f, 0.35f, 0.35f));
        SetFullSize(placeholder.rectTransform);
        placeholder.alignment = TextAnchor.MiddleCenter;
        inputField.placeholder = placeholder;
        return inputField;
    }

    // Square keys on a 1200 x 1000 page: 10 columns of 108 px, digits on top like a real keyboard, DEL filling
    // the rest of the last row. A small gap between keys makes each one easier to land on.
    static void CreateVirtualKeyboard(Transform parent, JoinMenu joinMenu)
    {
        const string keyValues = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const int columns = 10;
        const float left = 0.05f;
        const float top = 0.71f;
        const float keyWidth = 0.09f;
        const float keyHeight = 0.108f;
        const float gap = 6f;

        for (var index = 0; index < keyValues.Length; index++)
        {
            var keyButton = CreateButton(keyValues[index].ToString(), parent);
            keyButton.GetComponentInChildren<Text>().fontSize = 48;
            var row = index / columns;
            var column = index % columns;
            PlaceKey(keyButton, left + column * keyWidth, top - row * keyHeight, 1, keyWidth, keyHeight, gap);

            var key = keyButton.gameObject.AddComponent<KeyboardKey>();
            SetKeyboardKeyReference(key, joinMenu, keyValues[index].ToString());
            UnityEventTools.AddPersistentListener(keyButton.onClick, key.Press);
        }

        var lastColumn = keyValues.Length % columns;
        var lastRow = keyValues.Length / columns;
        var deleteButton = CreateButton("DEL", parent);
        PlaceKey(deleteButton, left + lastColumn * keyWidth, top - lastRow * keyHeight, columns - lastColumn, keyWidth, keyHeight, gap);
        UnityEventTools.AddPersistentListener(deleteButton.onClick, joinMenu.RemoveCharacter);
    }

    static void PlaceKey(Button key, float left, float top, int span, float keyWidth, float keyHeight, float gap)
    {
        var rect = key.GetComponent<RectTransform>();
        SetAnchors(rect, new Vector2(left, top - keyHeight), new Vector2(left + span * keyWidth, top));
        rect.offsetMin = new Vector2(gap, gap);
        rect.offsetMax = new Vector2(-gap, -gap);
    }

    static void SetKeyboardKeyReference(KeyboardKey key, JoinMenu joinMenu, string keyValue)
    {
        var serializedKey = new SerializedObject(key);
        serializedKey.FindProperty("joinMenu").objectReferenceValue = joinMenu;
        serializedKey.FindProperty("keyValue").stringValue = keyValue;
        serializedKey.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFullSize(RectTransform rectTransform)
    {
        SetAnchors(rectTransform, Vector2.zero, Vector2.one);
    }

    static void SetAnchors(RectTransform rectTransform, Vector2 min, Vector2 max)
    {
        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}