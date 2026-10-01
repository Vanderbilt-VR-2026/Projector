using System;
using Projector.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Room 1: the first thing a player sees. Host creates a session and takes everyone to the lobby;
// Join takes a lobby code typed on the in-headset keypad.
public class LandingMenu : MonoBehaviour
{
    const int CodeLength = 6;

    [SerializeField] Camera targetCamera;
    [SerializeField] Text codeText;
    [SerializeField] Text statusText;
    [SerializeField] Transform menuRoot;
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject joinPanel;

    string enteredCode = "";
    bool busy;

    void Start()
    {
        targetCamera = ResolveCamera();
        EnsureEventSystem();
        LockMenuToCamera();
        ShowMain();
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

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        joinPanel.SetActive(false);
        SetStatus("");
    }

    public void ShowJoin()
    {
        enteredCode = "";
        RefreshCode();
        mainPanel.SetActive(false);
        joinPanel.SetActive(true);
        SetStatus("");
    }

    public void AppendCodeCharacter(string character)
    {
        if (enteredCode.Length >= CodeLength)
            return;

        enteredCode += character;
        RefreshCode();
    }

    public void DeleteCodeCharacter()
    {
        if (enteredCode.Length == 0)
            return;

        enteredCode = enteredCode.Substring(0, enteredCode.Length - 1);
        RefreshCode();
    }

    public async void HostGame()
    {
        if (busy)
            return;

        busy = true;
        SetStatus("Creating lobby...");
        try
        {
            await SessionService.HostAsync();
            GameScenes.LoadForEveryone(GameScenes.Lobby);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            SetStatus("Could not host: " + exception.Message);
        }
        finally
        {
            busy = false;
        }
    }

    // The host's scene sync moves us into the lobby once Netcode connects.
    public async void JoinGame()
    {
        if (busy)
            return;

        if (enteredCode.Length != CodeLength)
        {
            SetStatus($"Enter the {CodeLength}-character lobby code.");
            return;
        }

        busy = true;
        SetStatus($"Joining {enteredCode}...");
        try
        {
            await SessionService.JoinAsync(enteredCode);
            SetStatus("Joined. Loading lobby...");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            SetStatus("Could not join: " + exception.Message);
        }
        finally
        {
            busy = false;
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void RefreshCode()
    {
        if (codeText != null)
            codeText.text = enteredCode.PadRight(CodeLength, '_');
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
