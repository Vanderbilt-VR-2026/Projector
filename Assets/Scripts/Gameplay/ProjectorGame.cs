using System.Linq;
using System.Collections.Generic;
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

        const float TableTop = 0.865f;
        [Tooltip("Item mock prefabs available to be placed on the table.")]
        [SerializeField] Object[] itemPrefabs;
        readonly List<GameObject> generatedItems = new List<GameObject>();
        int itemSeed;

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
            HideStaticProps();
            if (IsServer)
            {
                StartBuilding();
            }
            Apply(Phase.Value);
        }

        public override void OnNetworkDespawn()
        {
            Phase.OnValueChanged -= OnPhaseChanged;
            ClearGeneratedItems();
            if (projectorButton != null)
                projectorButton.selectEntered.RemoveListener(OnProjectorButton);
            if (Current == this)
                Current = null;
        }

        void OnPhaseChanged(GamePhase previous, GamePhase current) => Apply(current);

        void HideStaticProps()
        {
            var props = transform.parent != null ? transform.parent.Find("Props") : null;
            if (props == null)
                return;

            props.gameObject.SetActive(false);
        }

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
                    if (IsServer && buildEndTime.Value > 0 && remaining <= 0f)
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
            itemSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var playerCount = NetworkManager.ConnectedClientsIds.Count;
            buildEndTime.Value = Now + buildSeconds;
            Debug.Log($"Starting building with {playerCount} connected player(s) and {itemPrefabs?.Length ?? 0} item prefab reference(s).", this);
            Phase.Value = GamePhase.Building;
        }

        void GeneratePlayerItems()
        {
            if (!IsServer)
                return;

            ClearGeneratedItems();
            if (itemPrefabs == null || itemPrefabs.Length == 0)
            {
                Debug.LogError("No item mock prefabs are assigned to ProjectorGame.itemPrefabs.", this);
                return;
            }

            var clientIds = NetworkManager.ConnectedClientsIds.OrderBy(id => id).ToArray();
            var playerCount = clientIds.Length;
            var order = new List<int>(itemPrefabs.Length);
            for (var i = 0; i < itemPrefabs.Length; i++)
                if (itemPrefabs[i] != null)
                    order.Add(i);

            if (order.Count == 0)
            {
                Debug.LogError("ProjectorGame.itemPrefabs contains no valid GameObject references.", this);
                return;
            }

            var random = new System.Random(itemSeed);
            for (var i = order.Count - 1; i > 0; i--)
            {
                var swap = random.Next(i + 1);
                (order[i], order[swap]) = (order[swap], order[i]);
            }

            for (var i = 0; i < playerCount; i++)
            {
                var x = (i - (playerCount - 1) / 2f) * 0.45f;
                var prefabIndex = order[i % order.Count];
                var prefab = itemPrefabs[prefabIndex] as GameObject;
                var item = prefab != null
                    ? Instantiate(prefab, new Vector3(x, TableTop, 0.15f), Quaternion.identity)
                    : null;
                if (item == null)
                {
                    Debug.LogError("An item prefab did not instantiate as a GameObject.", this);
                    continue;
                }
                item.name = $"{prefab.name} (Player {i + 1})";
                var networkObject = item.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError($"Item prefab '{prefab.name}' is missing NetworkObject.", item);
                    Destroy(item);
                    continue;
                }
                generatedItems.Add(item);
                networkObject.SpawnWithOwnership(clientIds[i], true);
            }
            Debug.Log($"Generated {generatedItems.Count} item(s) for {playerCount} player(s).", this);
        }

        void ClearGeneratedItems()
        {
            if (!IsServer)
            {
                generatedItems.Clear();
                return;
            }

            for (var i = generatedItems.Count - 1; i >= 0; i--)
            {
                if (generatedItems[i] == null)
                    continue;
                var networkObject = generatedItems[i].GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                    networkObject.Despawn(true);
                else
                    Destroy(generatedItems[i]);
            }
            generatedItems.Clear();
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

            ProjectedLevel.Pack(shapes, out var points, out var counts, out var colors, out var kinds);
            BuildLevelRpc(points, counts, colors, kinds);
            Phase.Value = GamePhase.Racing;
            race.BeginRace();
            return true;
        }

        [Rpc(SendTo.Everyone)]
        void BuildLevelRpc(Vector2[] points, int[] counts, Color[] colors, int[] kinds)
        {
            platforms.BuildLevel(ProjectedLevel.Unpack(points, counts, colors, kinds));
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
            {
                GeneratePlayerItems();
                platforms.ClearLevel();
            }
            platforms.SetPreviewVisible(building);
            scoreboard.gameObject.SetActive(results);
            projectButton.SetActive(building);
            endRaceButton.SetActive(racing && IsServer);
            playAgainButton.SetActive(results && IsServer);
            leaveButton.SetActive(results);

            // Items are movable only during building, and the thumbstick drives the runner during the race.
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.None))
                prop.SetGrabAllowed(building);
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
