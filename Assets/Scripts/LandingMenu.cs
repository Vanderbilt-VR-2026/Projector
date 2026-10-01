using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class LandingMenu : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] Transform menuRoot;
    [SerializeField] Transform lobbyMenuRoot;
    [SerializeField] Transform joinMenuRoot;
    [SerializeField] Transform randomGameMenuRoot;
    [SerializeField] GameFoundMenu lobbyMenu;
    [SerializeField] JoinMenu joinMenu;
    [SerializeField] RandomGameMenu randomGameMenu;

    void Start()
    {
        targetCamera = ResolveCamera();
        EnsureEventSystem();
        EnsureMenuRoot();
        LockMenuToCamera();
        LockMenuToCamera(lobbyMenuRoot);
        LockMenuToCamera(joinMenuRoot);
        LockMenuToCamera(randomGameMenuRoot);
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

    public void HostPrivateGame()
    {
        HostGame(false);
    }

    public void HostPublicGame()
    {
        HostGame(true);
    }

    void HostGame(bool isPublic)
    {
        if (lobbyMenu == null || lobbyMenuRoot == null)
        {
            Debug.LogWarning("LandingMenu requires a lobby menu reference.", this);
            return;
        }

        lobbyMenu.ShowCode(isPublic);
        ShowMenu(lobbyMenuRoot);
    }

    public void JoinGameWithCode()
    {
        ShowMenu(joinMenuRoot);
    }

    public void JoinGameWithCode(string code)
    {
        Debug.Log($"Joining game with code {code}.", this);
    }

    public void JoinRandomGame()
    {
        if (randomGameMenu == null || randomGameMenuRoot == null)
        {
            Debug.LogWarning("LandingMenu requires a random game menu reference.", this);
            return;
        }

        ShowMenu(randomGameMenuRoot);
        randomGameMenu.BeginSearch();
    }

    public void ShowGameFoundMenu()
    {
        if (lobbyMenu == null || lobbyMenuRoot == null)
        {
            Debug.LogWarning("LandingMenu requires a lobby menu reference.", this);
            return;
        }

        lobbyMenu.ShowGameFound();
        ShowMenu(lobbyMenuRoot);
    }

    public void BackToLanding()
    {
        ShowMenu(menuRoot);
    }

    void ShowMenu(Transform activeMenu)
    {
        menuRoot.gameObject.SetActive(activeMenu == menuRoot);
        lobbyMenuRoot.gameObject.SetActive(activeMenu == lobbyMenuRoot);
        joinMenuRoot.gameObject.SetActive(activeMenu == joinMenuRoot);
        randomGameMenuRoot.gameObject.SetActive(activeMenu == randomGameMenuRoot);
    }
}