using System.Linq;
using Projector.Networking;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Projector.Gameplay
{
    public enum GamePhase
    {
        Building,
        Racing,
        Results
    }

    // The projector room's game loop, all in one room:
    //   Building - players hang props in the beam; the wall shows a live 2D projection of them.
    //   Racing   - PROJECT locks the shapes in as platforms and everyone races on the wall.
    //   Results  - the scoreboard is projected; the host can PLAY AGAIN (back to Building) or anyone can LEAVE.
    // The host can also END RACE at any point in a race to go straight to results.
    public class ProjectorGame : NetworkBehaviour
    {
        const float PreviewInterval = 0.1f;

        [SerializeField] float buildSeconds = 90f;
        [SerializeField] ProjectionCapture capture;
        [SerializeField] ProjectedPlatforms platforms;
        [SerializeField] RaceManager race;
        [SerializeField] Scoreboard scoreboard;
        [SerializeField] Text hudText;
        [SerializeField] GameObject projectButton;
        [SerializeField] GameObject endRaceButton;
        [SerializeField] GameObject playAgainButton;
        [SerializeField] GameObject leaveButton;
        [SerializeField] XRBaseInteractable projectorButton;
        [Tooltip("Where each player slot stands to watch the wall during the race.")]
        [SerializeField] Transform[] viewingSpots;

        public static ProjectorGame Current { get; private set; }

        public readonly NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>();
        readonly NetworkVariable<double> buildEndTime = new NetworkVariable<double>();
        float nextPreview;
        float noticeUntil;
        string notice;

        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Current = this;
            Phase.OnValueChanged += OnPhaseChanged;
            if (projectorButton != null)
                projectorButton.selectEntered.AddListener(OnProjectorButton);
            if (IsServer)
                StartBuilding();
            Apply(Phase.Value);
        }

        public override void OnNetworkDespawn()
        {
            Phase.OnValueChanged -= OnPhaseChanged;
            if (projectorButton != null)
                projectorButton.selectEntered.RemoveListener(OnProjectorButton);
            if (Current == this)
                Current = null;
        }

        void OnPhaseChanged(GamePhase previous, GamePhase current) => Apply(current);

        void OnProjectorButton(SelectEnterEventArgs args) => ProjectNow();

        // UI buttons and the projector's poke button.
        public void ProjectNow()
        {
            if (IsSpawned)
                RequestProjectRpc();
        }

        public void PlayAgain()
        {
            if (IsSpawned)
                RequestPlayAgainRpc();
        }

        public void EndRace()
        {
            if (IsSpawned)
                RequestEndRaceRpc();
        }

        public void Leave() => GameScenes.ReturnToLanding();

        void Update()
        {
            if (!IsSpawned)
                return;

            switch (Phase.Value)
            {
                case GamePhase.Building:
                    var remaining = Mathf.Max(0f, (float)(buildEndTime.Value - Now));
                    hudText.text = Time.time < noticeUntil
                        ? notice
                        : $"Hang objects in the projector beam\n{(int)remaining / 60}:{(int)remaining % 60:00}";
                    if (Time.time >= nextPreview)
                    {
                        nextPreview = Time.time + PreviewInterval;
                        platforms.ShowPreview(capture.Capture());
                    }
                    if (IsServer && remaining <= 0f)
                        Project(evenIfEmpty: true);
                    break;
                case GamePhase.Racing:
                    hudText.text = race.ClockText();
                    break;
                case GamePhase.Results:
                    hudText.text = IsServer ? "Results\nPLAY AGAIN to build a new level" : "Results\nWaiting for the host";
                    break;
            }
        }

        [Rpc(SendTo.Server)]
        void RequestProjectRpc(RpcParams rpcParams = default)
        {
            if (!Project(evenIfEmpty: false))
                NoticeRpc("Hang something in the beam first!", RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void NoticeRpc(string message, RpcParams rpcParams = default)
        {
            notice = message;
            noticeUntil = Time.time + 2.5f;
        }

        [Rpc(SendTo.Server)]
        void RequestPlayAgainRpc(RpcParams rpcParams = default)
        {
            if (Phase.Value != GamePhase.Results || rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
                return;
            race.ResetRace();
            StartBuilding();
        }

        [Rpc(SendTo.Server)]
        void RequestEndRaceRpc(RpcParams rpcParams = default)
        {
            if (Phase.Value == GamePhase.Racing && rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId)
                race.EndNow();
        }

        void StartBuilding()
        {
            NetworkProp.UnlockAll();
            buildEndTime.Value = Now + buildSeconds;
            Phase.Value = GamePhase.Building;
        }

        // Host: freeze the current projection as the level, send it to everyone, start the race. A button press
        // with an empty beam is refused (start and finish alone can't be crossed); the timer runs out regardless.
        bool Project(bool evenIfEmpty)
        {
            if (Phase.Value != GamePhase.Building)
                return true;

            var shapes = capture.Capture();
            if (shapes.Count == 0 && !evenIfEmpty)
                return false;

            ProjectedLevel.Pack(shapes, out var points, out var counts, out var colors);
            BuildLevelRpc(points, counts, colors);
            Phase.Value = GamePhase.Racing;
            race.BeginRace();
            return true;
        }

        [Rpc(SendTo.Everyone)]
        void BuildLevelRpc(Vector2[] points, int[] counts, Color[] colors)
        {
            platforms.BuildLevel(ProjectedLevel.Unpack(points, counts, colors));
        }

        // Host: called by the race when it ends.
        public void ShowResults(RaceManager.Result[] results)
        {
            ShowResultsRpc(results);
            Phase.Value = GamePhase.Results;
        }

        [Rpc(SendTo.Everyone)]
        void ShowResultsRpc(RaceManager.Result[] results)
        {
            scoreboard.Show(results.Select(result => new Scoreboard.Entry
            {
                playerName = result.Name.ToString(),
                points = result.Points,
                finishSeconds = result.Seconds
            }));
        }

        // Local presentation for each phase.
        void Apply(GamePhase phase)
        {
            var building = phase == GamePhase.Building;
            var racing = phase == GamePhase.Racing;
            var results = phase == GamePhase.Results;

            if (building)
                platforms.ClearLevel();
            platforms.SetPreviewVisible(building);
            scoreboard.gameObject.SetActive(results);
            projectButton.SetActive(building);
            endRaceButton.SetActive(racing && IsServer);
            playAgainButton.SetActive(results && IsServer);
            leaveButton.SetActive(results);

            // Props are frozen during the race, and the thumbstick drives the runner instead of the player.
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.None))
                prop.SetGrabAllowed(!racing);
            foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                provider.enabled = !racing;

            if (racing)
                FaceTheWall();
        }

        void FaceTheWall()
        {
            var origin = FindFirstObjectByType<XROrigin>();
            var mine = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None).FirstOrDefault(identity => identity.IsOwner && identity.GetComponent<NetworkAvatar>() != null);
            if (origin == null || viewingSpots.Length == 0)
                return;
            var spot = viewingSpots[(mine != null ? mine.Slot.Value : 0) % viewingSpots.Length];
            origin.transform.SetPositionAndRotation(spot.position, spot.rotation);
        }
    }
}
