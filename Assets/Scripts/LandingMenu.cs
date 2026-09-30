using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class LandingMenu : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] Transform menuRoot;
    [SerializeField] Transform hostMenuRoot;
    [SerializeField] HostMenu hostMenu;

    void Start()
    {
        targetCamera = ResolveCamera();
        EnsureEventSystem();
        EnsureMenuRoot();
        LockMenuToCamera();
        LockMenuToCamera(hostMenuRoot);
    }

    void LockMenuToCamera()
    {
        LockMenuToCamera(menuRoot);
    }

    void LockMenuToCamera(Transform root)
    {
        if (targetCamera == null)
            return;

        if (root == null)
            return;

        root.SetParent(targetCamera.transform, false);
        root.localPosition = new Vector3(0f, 0f, 2f);
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one * 0.001f;
    }

    void EnsureMenuRoot()
    {
        if (menuRoot == null)
            menuRoot = transform.Find("Landing Menu");
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
        if (hostMenu == null || hostMenuRoot == null)
        {
            Debug.LogWarning("LandingMenu requires a host menu reference.", this);
            return;
        }

        hostMenu.ShowCode();
        menuRoot.gameObject.SetActive(false);
        hostMenuRoot.gameObject.SetActive(true);
    }

    public void JoinGameWithCode()
    {
        Debug.Log("Join game with code selected.", this);
    }
}