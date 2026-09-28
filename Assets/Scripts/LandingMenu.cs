using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class LandingMenu : MonoBehaviour
{
    const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [SerializeField] Camera targetCamera;
    [SerializeField] Text codeText;
    [SerializeField] Transform menuRoot;

    void Start()
    {
        targetCamera = ResolveCamera();
        EnsureEventSystem();
        LockMenuToCamera();
    }

    void LockMenuToCamera()
    {
        if (targetCamera == null)
            return;

        if (menuRoot == null)
            menuRoot = transform.Find("Landing Menu");

        if (menuRoot == null)
            return;

        menuRoot.SetParent(targetCamera.transform, false);
        menuRoot.localPosition = new Vector3(0f, 0f, 2f);
        menuRoot.localRotation = Quaternion.identity;
        menuRoot.localScale = Vector3.one * 0.001f;
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

    public void HostGame()
    {
        if (codeText == null)
        {
            Debug.LogWarning("LandingMenu requires a code text reference.", this);
            return;
        }

        var code = new char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];

        codeText.text = new string(code);
    }
}