using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HostMenu : MonoBehaviour
{
    const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [SerializeField] Text codeText;
    [SerializeField] Text playerListText;

    readonly List<string> players = new List<string>();

    public void ShowCode()
    {
        if (codeText == null)
        {
            Debug.LogWarning("HostMenu requires a code text reference.", this);
            return;
        }

        var code = new char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];

        codeText.text = new string(code);
        players.Clear();
        AddPlayer("Host");
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
        Debug.Log("Start game selected for the current lobby.", this);
    }
}
