using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class LandingMenu : MonoBehaviour
{
    const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [SerializeField] Camera targetCamera;
    [SerializeField] float menuDistance = 2f;
    [SerializeField] Vector2 menuSize = new Vector2(1200f, 800f);

    Text codeText;

    void Start()
    {
        targetCamera = ResolveCamera();
        EnsureEventSystem();
        CreateMenu();
    }

    Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        var cameraOnObject = GetComponent<Camera>();
        if (cameraOnObject != null)
            return cameraOnObject;

        if (Camera.main != null)
            return Camera.main;

        return FindFirstObjectByType<Camera>();
    }

    void EnsureEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("XR Event System");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
    }

    void CreateMenu()
    {
        var canvasObject = new GameObject("Landing Menu");
        var menuParent = targetCamera != null ? targetCamera.transform : transform;
        canvasObject.transform.SetParent(menuParent, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, menuDistance);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.001f;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = targetCamera;
        canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = menuSize;

        var panel = CreateImage("Panel", canvasObject.transform, new Color(0.035f, 0.055f, 0.09f, 0.96f));
        SetFullSize(panel.rectTransform);

        var title = CreateText("PROJECTOR", panel.transform, 82, Color.white, TextAnchor.MiddleCenter);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.88f));

        var button = CreateButton("HOST GAME", panel.transform);
        SetAnchors(button.GetComponent<RectTransform>(), new Vector2(0.28f, 0.36f), new Vector2(0.72f, 0.54f));
        button.onClick.AddListener(HostGame);

        codeText = CreateText("", panel.transform, 64, new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter);
        SetAnchors(codeText.rectTransform, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.27f));
    }

    void HostGame()
    {
        var code = new char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];

        codeText.text = new string(code);
    }

    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static Text CreateText(string value, Transform parent, int fontSize, Color color, TextAnchor alignment)
    {
        var textObject = new GameObject(string.IsNullOrEmpty(value) ? "Code" : value);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    static Button CreateButton(string label, Transform parent)
    {
        var buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.1f, 0.45f, 0.62f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText(label, buttonObject.transform, 36, Color.white, TextAnchor.MiddleCenter);
        SetFullSize(text.rectTransform);
        return button;
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