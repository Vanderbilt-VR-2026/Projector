using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class LandingMenuBuilder
{
    const string XROriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
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
        var xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        if (xrOriginPrefab == null)
        {
            Debug.LogError($"Could not find XR Origin prefab at {XROriginPrefabPath}.");
            return;
        }

        PrefabUtility.InstantiatePrefab(xrOriginPrefab, scene);
            CreateDirectionalLight();
        CreateFloor();
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

            CreateLandingMenuObjects();
        }

    public static void RebuildLandingMenuScene()
    {
        var scene = EditorSceneManager.OpenScene(LandingMenuScenePath, OpenSceneMode.Single);
        RebuildLandingMenuObjects();
        EditorSceneManager.SaveScene(scene, LandingMenuScenePath);
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
        var existingHostMenu = controller.transform.Find("Host Menu");
        var existingJoinMenu = controller.transform.Find("Join Game Menu");
        if (existingMenu != null && existingHostMenu != null && existingJoinMenu != null)
        {
            Selection.activeGameObject = existingMenu.gameObject;
            return;
        }

        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var landingMenu = CreateCanvas("Landing Menu", controller.transform, camera);
        var panel = CreateImage("Panel", landingMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(panel.rectTransform);

        var title = CreateText("PROJECTOR", panel.transform, 82, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.88f));

        var button = CreateButton("HOST GAME", panel.transform);
        SetAnchors(button.GetComponent<RectTransform>(), new Vector2(0.28f, 0.36f), new Vector2(0.72f, 0.54f));
        UnityEventTools.AddPersistentListener(button.onClick, controller.HostGame);

        var joinButton = CreateButton("JOIN GAME WITH CODE", panel.transform);
        SetAnchors(joinButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.36f));
        UnityEventTools.AddPersistentListener(joinButton.onClick, controller.JoinGameWithCode);

        var codeText = CreateText("", panel.transform, 64, new Color(1f, 0.8f, 0.3f));
        SetAnchors(codeText.rectTransform, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.27f));

        var hostMenu = CreateCanvas("Host Menu", controller.transform, camera);
        hostMenu.gameObject.SetActive(false);
        var hostPanel = CreateImage("Panel", hostMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(hostPanel.rectTransform);

        var hostCodeText = CreateText("", hostPanel.transform, 64, new Color(1f, 0.8f, 0.3f));
        SetAnchors(hostCodeText.rectTransform, new Vector2(0.15f, 0.65f), new Vector2(0.85f, 0.9f));

        var playerListTitle = CreateText("CURRENT PLAYERS", hostPanel.transform, 34, Color.white);
        SetAnchors(playerListTitle.rectTransform, new Vector2(0.2f, 0.54f), new Vector2(0.8f, 0.63f));

        var playerListText = CreateText("", hostPanel.transform, 34, Color.white);
        SetAnchors(playerListText.rectTransform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.54f));
        playerListText.alignment = TextAnchor.UpperCenter;

        var hostMenuController = hostMenu.AddComponent<HostMenu>();
        var hostBackButton = CreateButton("BACK", hostPanel.transform);
        SetAnchors(hostBackButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.24f, 0.15f));
        UnityEventTools.AddPersistentListener(hostBackButton.onClick, controller.BackToLanding);

        var startGameButton = CreateButton("START GAME", hostPanel.transform);
        SetAnchors(startGameButton.GetComponent<RectTransform>(), new Vector2(0.76f, 0.06f), new Vector2(0.94f, 0.15f));
        startGameButton.GetComponentInChildren<Text>().fontSize = 22;
        UnityEventTools.AddPersistentListener(startGameButton.onClick, hostMenuController.StartGame);

        var joinMenu = CreateCanvas("Join Game Menu", controller.transform, camera);
        joinMenu.gameObject.SetActive(false);
        var joinPanel = CreateImage("Panel", joinMenu.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(joinPanel.rectTransform);

        var joinTitle = CreateText("JOIN GAME", joinPanel.transform, 64, Color.white);
        SetAnchors(joinTitle.rectTransform, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.92f));

        var codeInput = CreateInputField(joinPanel.transform);
        SetAnchors(codeInput.GetComponent<RectTransform>(), new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.77f));

        var joinMenuController = joinMenu.AddComponent<JoinMenu>();
        CreateVirtualKeyboard(joinPanel.transform, joinMenuController);

        var submitButton = CreateButton("JOIN", joinPanel.transform);
        SetAnchors(submitButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.27f));

        UnityEventTools.AddPersistentListener(submitButton.onClick, joinMenuController.SubmitJoinCode);

        var joinBackButton = CreateButton("BACK", joinPanel.transform);
        SetAnchors(joinBackButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.24f, 0.15f));
        UnityEventTools.AddPersistentListener(joinBackButton.onClick, controller.BackToLanding);

        SetPrivateReferences(controller, camera, landingMenu.transform, hostMenu.transform, joinMenu.transform, hostMenuController, hostCodeText, playerListText, joinMenuController, codeInput);
        EnsureEventSystem();
        Selection.activeGameObject = landingMenu;
        EditorSceneManager.MarkSceneDirty(landingMenu.scene);
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

    static void SetPrivateReferences(LandingMenu controller, Camera camera, Transform menuRoot, Transform hostMenuRoot, Transform joinMenuRoot, HostMenu hostMenu, Text hostCodeText, Text playerListText, JoinMenu joinMenu, InputField codeInput)
    {
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("targetCamera").objectReferenceValue = camera;
        serializedController.FindProperty("menuRoot").objectReferenceValue = menuRoot;
        serializedController.FindProperty("hostMenuRoot").objectReferenceValue = hostMenuRoot;
        serializedController.FindProperty("joinMenuRoot").objectReferenceValue = joinMenuRoot;
        serializedController.FindProperty("hostMenu").objectReferenceValue = hostMenu;
        serializedController.FindProperty("joinMenu").objectReferenceValue = joinMenu;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        var serializedHostMenu = new SerializedObject(hostMenu);
        serializedHostMenu.FindProperty("codeText").objectReferenceValue = hostCodeText;
        serializedHostMenu.FindProperty("playerListText").objectReferenceValue = playerListText;
        serializedHostMenu.ApplyModifiedPropertiesWithoutUndo();

        var serializedJoinMenu = new SerializedObject(joinMenu);
        serializedJoinMenu.FindProperty("landingMenu").objectReferenceValue = controller;
        serializedJoinMenu.FindProperty("codeInput").objectReferenceValue = codeInput;
        serializedJoinMenu.ApplyModifiedPropertiesWithoutUndo();
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

    static void CreateVirtualKeyboard(Transform parent, JoinMenu joinMenu)
    {
        const string keyValues = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const int columns = 6;
        const float left = 0.08f;
        const float right = 0.92f;
        const float bottom = 0.29f;
        const float top = 0.65f;
        var keyWidth = (right - left) / columns;
        var keyHeight = (top - bottom) / 6f;

        for (var index = 0; index < keyValues.Length; index++)
        {
            var keyButton = CreateButton(keyValues[index].ToString(), parent);
            var row = index / columns;
            var column = index % columns;
            var min = new Vector2(left + column * keyWidth, top - (row + 1) * keyHeight);
            var max = new Vector2(left + (column + 1) * keyWidth, top - row * keyHeight);
            SetAnchors(keyButton.GetComponent<RectTransform>(), min, max);

            var key = keyButton.gameObject.AddComponent<KeyboardKey>();
            SetKeyboardKeyReference(key, joinMenu, keyValues[index].ToString());
            UnityEventTools.AddPersistentListener(keyButton.onClick, key.Press);
        }

        var deleteButton = CreateButton("DEL", parent);
        SetAnchors(deleteButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.18f), new Vector2(0.24f, 0.27f));
        UnityEventTools.AddPersistentListener(deleteButton.onClick, joinMenu.RemoveCharacter);
    }

    static void SetKeyboardKeyReference(KeyboardKey key, JoinMenu joinMenu, string keyValue)
    {
        var serializedKey = new SerializedObject(key);
        serializedKey.FindProperty("joinMenu").objectReferenceValue = joinMenu;
        serializedKey.FindProperty("keyValue").stringValue = keyValue;
        serializedKey.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateDirectionalLight()
    {
        var lightObject = new GameObject("Directional Light");
        Undo.RegisterCreatedObjectUndo(lightObject, "Create Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
    }

    static void CreateFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(floor, "Create XR Floor");
        floor.name = "XR Floor";
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
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