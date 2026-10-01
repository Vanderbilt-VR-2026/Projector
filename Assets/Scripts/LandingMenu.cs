using System;
using System.Threading.Tasks;
using Projector.Networking;
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
    [SerializeField] Text statusText;

    bool busy;
    bool leaving;

    void OnEnable() => SessionService.SessionChanged += OnSessionChanged;

    void OnDisable() => SessionService.SessionChanged -= OnSessionChanged;

    // The host closed the game while we were waiting on the game-found screen.
    void OnSessionChanged()
    {
        if (SessionService.Current != null || leaving || lobbyMenuRoot == null || !lobbyMenuRoot.gameObject.activeSelf)
            return;
        ShowMenu(menuRoot);
        SetStatus("The host closed the game.");
    }

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

    // Creates a real session (public ones can be found by JOIN RANDOM GAME) and shows its code and players.
    void HostGame(bool isPublic)
    {
        if (lobbyMenu == null || lobbyMenuRoot == null)
        {
            Debug.LogWarning("LandingMenu requires a lobby menu reference.", this);
            return;
        }

        RunSessionTask(isPublic ? "Creating public game..." : "Creating private game...", () => SessionService.HostAsync(isPrivate: !isPublic), "Could not host");
    }

    public void JoinGameWithCode()
    {
        ShowMenu(joinMenuRoot);
    }

    public void JoinGameWithCode(string code)
    {
        BackToLanding();
        RunSessionTask($"Joining {code}...", () => SessionService.JoinAsync(code), "Could not join");
    }

    // Shared by host / join / random: show progress, run the session call, then the game-found screen or the error.
    public async void RunSessionTask(string progress, Func<Task> sessionCall, string failurePrefix)
    {
        if (busy)
            return;

        busy = true;
        SetStatus(progress);
        try
        {
            await sessionCall();
            SetStatus("");
            ShowGameFoundMenu();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            ShowMenu(menuRoot);
            SetStatus($"{failurePrefix}: {exception.Message}");
        }
        finally
        {
            busy = false;
        }
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

        ShowMenu(lobbyMenuRoot);
        lobbyMenu.ShowSession();
    }

    // BACK from any sub-menu; leaving the game-found screen also leaves the session.
    public async void BackToLanding()
    {
        ShowMenu(menuRoot);
        if (SessionService.Current == null)
            return;
        leaving = true;
        await SessionService.LeaveAsync();
        leaving = false;
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    void ShowMenu(Transform activeMenu)
    {
        menuRoot.gameObject.SetActive(activeMenu == menuRoot);
        lobbyMenuRoot.gameObject.SetActive(activeMenu == lobbyMenuRoot);
        joinMenuRoot.gameObject.SetActive(activeMenu == joinMenuRoot);
        randomGameMenuRoot.gameObject.SetActive(activeMenu == randomGameMenuRoot);
    }
}