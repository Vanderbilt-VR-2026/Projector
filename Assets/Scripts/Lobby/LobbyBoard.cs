using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectorGame.Lobby
{
    /// <summary>
    /// The "waiting for players" board in the lobby: room code, four player slots, status line and a host start countdown.
    /// Networking/menus can drive it through SetRoomCode / SetPlayers / AddPlayer / RemovePlayer.
    /// With demoMode on (default until networking lands) it simulates players joining so the room can be tested solo.
    /// </summary>
    public class LobbyBoard : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Header("UI")]
        public Text codeText;
        public Text statusText;
        public Text countText;
        public Text[] slotTexts = new Text[MaxPlayers];
        public Image[] slotDots = new Image[MaxPlayers];

        [Header("Players")]
        public Color[] playerColors =
        {
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 0.6f, 1f),
            new Color(0.35f, 0.9f, 0.4f),
            new Color(1f, 0.85f, 0.2f),
        };
        public Color emptySlotColor = new Color(0.25f, 0.25f, 0.3f);
        public SpawnPad[] pads;

        [Header("Testing")]
        [Tooltip("Simulate other players joining until real networking is hooked up.")]
        public bool demoMode = true;
        public float demoJoinInterval = 4f;
        public string[] demoNames = { "You", "Eileen", "Jason", "David" };

        [Header("Start")]
        [Tooltip("Scene loaded when the countdown finishes (only if it is in the build).")]
        public string gameSceneName = "ProjectorRoom";
        public int countdownSeconds = 3;

        readonly List<string> m_Players = new List<string>();
        float m_NextDemoJoin;
        bool m_CountingDown;
        float m_CountdownEnd;
        string m_StatusOverride;
        float m_StatusOverrideUntil;

        public IReadOnlyList<string> Players => m_Players;

        void Start()
        {
            SetRoomCode(GenerateCode());
            if (demoMode && demoNames.Length > 0)
            {
                AddPlayer(demoNames[0]);
                m_NextDemoJoin = Time.time + demoJoinInterval;
            }
            Refresh();
        }

        void Update()
        {
            if (demoMode && !m_CountingDown && m_Players.Count < MaxPlayers && m_Players.Count < demoNames.Length && Time.time >= m_NextDemoJoin)
            {
                AddPlayer(demoNames[m_Players.Count]);
                m_NextDemoJoin = Time.time + demoJoinInterval;
            }

            if (m_CountingDown)
            {
                int remaining = Mathf.CeilToInt(m_CountdownEnd - Time.time);
                if (remaining <= 0)
                {
                    m_CountingDown = false;
                    if (!SceneLoader.TryLoad(gameSceneName))
                        Flash("Game room isn't in this build yet", 3f);
                }
                else
                {
                    SetStatus("Starting in " + remaining + "...");
                }
                return;
            }

            if (Time.time < m_StatusOverrideUntil)
            {
                SetStatus(m_StatusOverride);
                return;
            }

            if (m_Players.Count >= MaxPlayers)
            {
                SetStatus("Lobby full - host can start!");
            }
            else
            {
                int dots = (int)(Time.time * 2f) % 4;
                SetStatus("Waiting for players" + new string('.', dots));
            }
        }

        // ------------------------------------------------------------ public API

        public void SetRoomCode(string code)
        {
            if (codeText != null)
                codeText.text = "CODE  " + code;
        }

        public void SetPlayers(IList<string> players)
        {
            m_Players.Clear();
            foreach (var p in players)
                if (m_Players.Count < MaxPlayers && !string.IsNullOrWhiteSpace(p))
                    m_Players.Add(p);
            Refresh();
        }

        public void AddPlayer(string playerName)
        {
            if (m_Players.Count >= MaxPlayers || string.IsNullOrWhiteSpace(playerName) || m_Players.Contains(playerName))
                return;
            m_Players.Add(playerName);
            Refresh();
            if (m_Players.Count > 1)
                Flash(playerName + " joined!", 2f);
        }

        public void RemovePlayer(string playerName)
        {
            if (m_Players.Remove(playerName))
                Refresh();
        }

        /// <summary>Hooked to the host's START button.</summary>
        public void StartCountdown()
        {
            if (m_CountingDown)
                return;
            if (m_Players.Count < 2)
            {
                Flash("Need at least 2 players to start", 2.5f);
                return;
            }
            m_CountingDown = true;
            m_CountdownEnd = Time.time + countdownSeconds;
        }

        /// <summary>Clears the simulated players and starts the demo over.</summary>
        public void ResetLobby()
        {
            m_CountingDown = false;
            m_Players.Clear();
            SetRoomCode(GenerateCode());
            if (demoMode && demoNames.Length > 0)
                AddPlayer(demoNames[0]);
            m_NextDemoJoin = Time.time + demoJoinInterval;
            Refresh();
            Flash("New lobby created", 2f);
        }

        // ------------------------------------------------------------ helpers

        void Refresh()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                bool filled = i < m_Players.Count;
                if (slotTexts != null && i < slotTexts.Length && slotTexts[i] != null)
                    slotTexts[i].text = filled ? m_Players[i] : "open slot";
                if (slotDots != null && i < slotDots.Length && slotDots[i] != null)
                    slotDots[i].color = filled ? playerColors[i % playerColors.Length] : emptySlotColor;
                if (pads != null && i < pads.Length && pads[i] != null)
                    pads[i].SetClaimed(filled && i > 0);
            }
            if (countText != null)
                countText.text = m_Players.Count + " / " + MaxPlayers + " players";
        }

        void Flash(string message, float seconds)
        {
            m_StatusOverride = message;
            m_StatusOverrideUntil = Time.time + seconds;
        }

        void SetStatus(string s)
        {
            if (statusText != null && statusText.text != s)
                statusText.text = s;
        }

        static string GenerateCode()
        {
            var code = new char[6];
            for (int i = 0; i < code.Length; i++)
                code[i] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];
            return new string(code);
        }
    }
}
