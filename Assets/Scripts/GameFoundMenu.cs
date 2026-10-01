using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameFoundMenu : MonoBehaviour
{
    const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [SerializeField] Text codeText;
    [SerializeField] Text playerListText;

    readonly List<string> players = new List<string>();

    public string CurrentCode { get; private set; }
    public bool IsPublic { get; private set; }

    public void ShowCode(bool isPublic)
    {
        if (codeText != null)
        {
            var code = new char[6];
            for (var index = 0; index < code.Length; index++)
                code[index] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];

            CurrentCode = new string(code);
            IsPublic = isPublic;
            codeText.text = CurrentCode;
        }

        players.Clear();
        AddPlayer("Host");
    }

    public void ShowGameFound()
    {
        ShowCode(false);
        players.Clear();
        AddPlayer("You");
    }

    public void AddPlayer(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || players.Contains(playerName))
            return;

        players.Add(playerName);
        if (playerListText != null)
            playerListText.text = string.Join("\n", players);
    }

    public void StartGame()
    {
        Debug.Log("Start game selected for the found game.", this);
    }
}