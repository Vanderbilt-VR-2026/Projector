using System.Text;
using Projector.Networking;
using UnityEngine;
using UnityEngine.UI;

// Waiting room board: shows the lobby code and who has joined. Only the host can start the game.
public class LobbyMenu : MonoBehaviour
{
    [SerializeField] Text codeText;
    [SerializeField] Text playersText;
    [SerializeField] Text statusText;
    [SerializeField] Button startButton;

    void OnEnable()
    {
        SessionService.SessionChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        SessionService.SessionChanged -= Refresh;
    }

    void Refresh()
    {
        var session = SessionService.Current;
        if (session == null)
        {
            // Scene opened directly in the editor: allow starting solo so the flow can still be tested.
            codeText.text = "OFFLINE";
            playersText.text = "No session.\nHost from the Landing Menu to invite players.";
            statusText.text = "Start loads the projector room locally.";
            startButton.gameObject.SetActive(true);
            return;
        }

        codeText.text = session.Code;

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
        playersText.text = players.ToString();

        startButton.gameObject.SetActive(session.IsHost);
        statusText.text = session.IsHost
            ? $"{session.PlayerCount}/{session.MaxPlayers} players. Start when everyone is in."
            : "Waiting for the host to start...";
    }

    public async void StartGame()
    {
        if (!SessionService.IsHost)
            return;

        startButton.interactable = false;

        // Lock the session so nobody joins mid-game. Failing to lock shouldn't stop the game from starting.
        var session = SessionService.Current;
        if (session != null)
        {
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
        }

        GameScenes.LoadForEveryone(GameScenes.ProjectorRoom);
    }

    public void LeaveLobby()
    {
        GameScenes.ReturnToLanding();
    }
}
