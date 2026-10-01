using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// End-of-race results, drawn on the projection. The game calls Show() with real results; with showMockOnStart
// it previews mock entries instead. A negative finish time means the player did not finish.
public class Scoreboard : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public string playerName;
        public int points;
        public float finishSeconds;
    }

    [SerializeField] Text winnerText;
    [SerializeField] Text rankColumn;
    [SerializeField] Text nameColumn;
    [SerializeField] Text pointsColumn;
    [SerializeField] Text timeColumn;
    [SerializeField] bool showMockOnStart = true;
    [SerializeField] List<Entry> mockEntries = new List<Entry>
    {
        new Entry { playerName = "Player 1", points = 1250, finishSeconds = 74.2f },
        new Entry { playerName = "Player 2", points = 980, finishSeconds = 81.6f },
        new Entry { playerName = "Player 3", points = 980, finishSeconds = 88.9f },
        new Entry { playerName = "Player 4", points = 410, finishSeconds = 102.3f }
    };

    // Only the mock preview is drawn here: the board is first shown when results arrive, and Start must not
    // overwrite them.
    void Start()
    {
        if (showMockOnStart)
            ShowMockResults();
    }

    public void ShowMockResults()
    {
        Show(mockEntries);
    }

    // Most points wins; ties go to the faster finish, and finishers beat non-finishers.
    public void Show(IEnumerable<Entry> results)
    {
        var ranked = results.OrderByDescending(entry => entry.points)
            .ThenBy(entry => entry.finishSeconds < 0f ? float.MaxValue : entry.finishSeconds).ToList();

        var ranks = new StringBuilder();
        var names = new StringBuilder();
        var points = new StringBuilder();
        var times = new StringBuilder();
        for (var index = 0; index < ranked.Count; index++)
        {
            ranks.AppendLine((index + 1).ToString());
            names.AppendLine(ranked[index].playerName);
            points.AppendLine(ranked[index].points.ToString());
            times.AppendLine(FormatTime(ranked[index].finishSeconds));
        }

        rankColumn.text = ranks.ToString();
        nameColumn.text = names.ToString();
        pointsColumn.text = points.ToString();
        timeColumn.text = times.ToString();
        winnerText.text = ranked.Count > 0 ? $"WINNER: {ranked[0].playerName}" : "";
    }

    static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            return "DNF";

        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
    }
}
