using System.Text;
using Projector.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// The last stop before the game: shows the session's code and who has joined, live. Only the host gets
// START GAME, which locks the session and takes everyone to the projector room. START waits until every
// listed player is also connected over Netcode (they appear in the session a moment before that), or a
// player who just joined would be left behind.
public class GameFoundMenu : MonoBehaviour
{
    [SerializeField] Text codeText;
    [SerializeField] Text playerListText;
    [SerializeField] Button startButton;

    bool starting;
    bool everyoneConnected;

    void OnEnable()
    {
        SessionService.SessionChanged += Refresh;
    }

    void OnDisable()
    {
        SessionService.SessionChanged -= Refresh;
    }

    public void ShowSession()
    {
        starting = false;
        Refresh();
    }

    void Refresh()
    {
        var session = SessionService.Current;
        if (session == null)
        {
            codeText.text = "";
            playerListText.text = "Not in a game.";
            if (startButton != null)
                startButton.gameObject.SetActive(false);
            return;
        }

        codeText.text = session.IsPrivate ? $"CODE: {session.Code}" : $"PUBLIC GAME  -  CODE: {session.Code}";

        var players = new StringBuilder();
        for (var index = 0; index < session.Players.Count; index++)
        {
            var player = session.Players[index];
            players.Append(SessionService.GetDisplayName(player, index));
            if (player.Id == session.Host)
                players.Append("  (host)");
            if (session.CurrentPlayer != null && player.Id == session.CurrentPlayer.Id)
                players.Append("  (you)");
            players.AppendLine();
        }
        if (!session.IsHost)
            players.AppendLine().Append("Waiting for the host to start...");
        else if (!everyoneConnected)
            players.AppendLine().Append("Connecting players...");
        playerListText.text = players.ToString();

        if (startButton != null)
        {
            startButton.gameObject.SetActive(session.IsHost);
            startButton.interactable = !starting && everyoneConnected;
        }
    }

    // Netcode connections don't raise session events, so check them each frame.
    void Update()
    {
        var session = SessionService.Current;
        var manager = NetworkManager.Singleton;
        var connected = session != null && session.IsHost && manager != null && manager.IsServer
                        && manager.ConnectedClientsIds.Count >= session.PlayerCount;
        if (connected != everyoneConnected)
        {
            everyoneConnected = connected;
            Refresh();
        }
    }

    public async void StartGame()
    {
        var session = SessionService.Current;
        if (session == null || !session.IsHost || starting || !everyoneConnected)
            return;

        starting = true;
        Refresh();

        // Lock the session so nobody joins mid-game. Failing to lock shouldn't stop the game from starting.
        try
        {
            var host = session.AsHost();
            host.IsLocked = true;
            await host.SavePropertiesAsync();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Could not lock the session: {exception.Message}", this);
        }

        GameScenes.LoadForEveryone(GameScenes.ProjectorRoom);
    }
}
