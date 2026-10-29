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
    //   Racing   - pulling the rope (or the projector button) locks the shapes in as platforms and everyone
    //              races on the wall.
    //   Results  - the scoreboard is projected; the host pulls the rope to play again (back to Building), and
    //              everyone else pulls it to leave.
    // The host can also pull the rope at any point in a race to end it and go straight to results. In any phase,
    // pulling twice in quick succession leaves: the curtains close and the player is taken back to the lobby.
    // What a pull does right now is written in the square of light beside the rope.
    public class ProjectorGame : NetworkBehaviour
    {
        const float PreviewInterval = 0.1f;

        [SerializeField] float buildSeconds = 90f;
        [SerializeField] ProjectionCapture capture;
        [SerializeField] ProjectedPlatforms platforms;
        [SerializeField] RaceManager race;
        [SerializeField] Scoreboard scoreboard;
        [Tooltip("The instructions in the square of light beside the rope.")]
        [SerializeField] Text hudText;
        [SerializeField] PullRope rope;
        [SerializeField] XRBaseInteractable projectorButton;
        [Tooltip("Where each player slot stands to watch the wall during the race.")]
        [SerializeField] Transform[] viewingSpots;

        const float TableTop = 0.865f;
        [Tooltip("Item mock prefabs available to be placed on the table.")]
        // GameObject[], not Object[]: a prefab stored in an Object slot serializes as the prefab asset container,
        // which loads as a nameless non-GameObject, so nothing could be instantiated from it.
        [SerializeField] GameObject[] itemPrefabs;
        [Tooltip("Items each player gets on the table at the start of every build phase.")]
        [SerializeField, Min(1)] int itemsPerPlayer = 3;

        // Table positions (x from the player's spot, z on the table) for a player's items, in order.
        static readonly Vector2[] ItemOffsets = { new Vector2(-0.2f, 0.15f), new Vector2(0.2f, 0.15f), new Vector2(0f, 0.45f) };
        readonly List<GameObject> generatedItems = new List<GameObject>();
        int itemSeed;

        public static ProjectorGame Current { get; private set; }

        public readonly NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>();
        readonly NetworkVariable<double> buildEndTime = new NetworkVariable<double>();
        const float SecondPullSeconds = 2.5f;
        const string LeaveHint = "Pull twice to leave";

        float nextPreview;
        float noticeUntil;
        // Host, in results: after one pull, play again when this time passes unless a second pull leaves first.
        float playAgainAt = -1f;
        float lastPullAt = float.NegativeInfinity;
        string notice;

        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Current = this;
            Phase.OnValueChanged += OnPhaseChanged;
            if (projectorButton != null)
                projectorButton.selectEntered.AddListener(OnProjectorButton);
            if (rope != null)
                rope.Pulled += OnRopePulled;
            HideStaticProps();
            if (IsServer)
            {
                // Items spawned while a player is still loading this scene never reach that player, so with
                // others joining, start building only once everyone has loaded (or after a timeout).
                if (NetworkManager.ConnectedClientsIds.Count > 1)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted += OnEveryoneLoaded;
                    StartCoroutine(StartBuildingAfterTimeout());
                }
                else
                    StartBuilding();
            }
            Apply(Phase.Value);
        }

        const float LoadTimeoutSeconds = 20f;
        bool buildStarted;

        void OnEveryoneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == gameObject.scene.name)
                StartFirstBuild();
        }

        System.Collections.IEnumerator StartBuildingAfterTimeout()
        {
            yield return new WaitForSeconds(LoadTimeoutSeconds);
            StartFirstBuild();
        }

        void StartFirstBuild()
        {
            if (buildStarted)
                return;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnEveryoneLoaded;
            StartBuilding();
        }

        public override void OnNetworkDespawn()
        {
            Phase.OnValueChanged -= OnPhaseChanged;
            if (IsServer && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnEveryoneLoaded;
            ClearGeneratedItems();
            if (projectorButton != null)
                projectorButton.selectEntered.RemoveListener(OnProjectorButton);
            if (rope != null)
                rope.Pulled -= OnRopePulled;
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

        void OnRopePulled()
        {
            // Two pulls in quick succession leave, whatever is on the screen.
            if (Time.time - lastPullAt < SecondPullSeconds)
            {
                playAgainAt = -1f;
                Leave();
                return;
            }
            lastPullAt = Time.time;

            switch (Phase.Value)
            {
                case GamePhase.Building:
                    ProjectNow();
                    break;
                case GamePhase.Racing:
                    EndRace();
                    break;
                case GamePhase.Results:
                    if (!IsServer)
                        Leave();
                    else
                        playAgainAt = Time.time + SecondPullSeconds;
                    break;
            }
        }

        // The rope and the projector's poke button.
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
                    hudText.text = Time.time < noticeUntil ? notice
                        : buildEndTime.Value <= 0 ? "Waiting for players...\n" + LeaveHint
                        : $"Pull to start\n{LeaveHint}\n{(int)remaining / 60}:{(int)remaining % 60:00}";
                    if (Time.time >= nextPreview)
                    {
                        nextPreview = Time.time + PreviewInterval;
                        platforms.ShowPreview(capture.Capture());
                    }
                    if (IsServer && buildEndTime.Value > 0 && remaining <= 0f)
                        Project(evenIfEmpty: true);
                    break;
                case GamePhase.Racing:
                    hudText.text = (IsServer ? "Pull to end race\n" : "") + LeaveHint + "\n" + race.ClockText();
                    break;
                case GamePhase.Results:
                    if (!IsServer)
                        hudText.text = "Pull to leave";
                    else if (playAgainAt < 0f)
                        hudText.text = "Pull to play again\nPull twice to leave";
                    else if (Time.time < playAgainAt)
                        hudText.text = "Playing again...\nPull again to leave";
                    else
                    {
                        playAgainAt = -1f;
                        PlayAgain();
                    }
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
            buildStarted = true;
            Phase.Value = GamePhase.Building;
            // Here rather than in Apply: Apply also runs the moment the room spawns, before joiners have loaded.
            GeneratePlayerItems();
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

            // Each player gets itemsPerPlayer different items, laid out in front of their own spot at the table
            // (spots are 0.8 m apart): two side by side at the edge, the next one just behind.
            var itemIndex = 0;
            for (var i = 0; i < playerCount; i++)
            {
                var spotX = (PlayerRoster.SlotFor(clientIds[i]) - 1.5f) * 0.8f;
                for (var k = 0; k < itemsPerPlayer; k++, itemIndex++)
                {
                    var offset = ItemOffsets[k % ItemOffsets.Length];
                    var prefabIndex = order[itemIndex % order.Count];
                    var prefab = itemPrefabs[prefabIndex];
                    var item = prefab != null
                        ? Instantiate(prefab, new Vector3(spotX + offset.x, TableTop, offset.y), Quaternion.identity)
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

            playAgainAt = -1f;
            if (building)
                platforms.ClearLevel();
            platforms.SetPreviewVisible(building);
            scoreboard.gameObject.SetActive(results);

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
