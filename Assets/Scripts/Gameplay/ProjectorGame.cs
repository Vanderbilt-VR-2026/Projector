using System.Linq;
using System.Collections.Generic;
using Projector.Audio;
using Projector.Networking;
using global::ProjectorGame.Items;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
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
    //   Racing   - everyone confirming in their HUD locks the shapes in as platforms and everyone
    //              races on the wall.
    //   Results  - the scoreboard is projected; the host pulls the rope to play again (back to Building), and
    //              everyone else pulls it to leave.
    // The host can also pull the rope at any point in a race to end it and go straight to results. In any phase,
    // pulling twice in quick succession leaves: the curtains close and the player is taken back to the lobby.
    // What a pull does right now is written in the square of light beside the rope.
    public class ProjectorGame : NetworkBehaviour
    {
        const float PreviewInterval = 0.1f;

        [SerializeField] ProjectionCapture capture;
        [SerializeField] ProjectedPlatforms platforms;
        [SerializeField] RaceManager race;
        [SerializeField] Scoreboard scoreboard;
        [Tooltip("The instructions in the square of light beside the rope.")]
        [SerializeField] Text hudText;
        [SerializeField] PullRope rope;
        [Tooltip("Where each player slot stands to watch the wall during the race.")]
        [SerializeField] Transform[] viewingSpots;

        const float TableTop = 0.865f;
        [Tooltip("Item mock prefabs available to be placed on the table.")]
        // GameObject[], not Object[]: a prefab stored in an Object slot serializes as the prefab asset container,
        // which loads as a nameless non-GameObject, so nothing could be instantiated from it.
        [SerializeField] GameObject[] itemPrefabs;
        [Tooltip("Opening rounds that give two Starter items per player. Later rounds give one Starter or Advanced item.")]
        [SerializeField, Range(1, 2)] int starterRounds = 2;

        // Table positions (x from the player's spot, z on the table) for a player's items, in order.
        static readonly Vector2[] ItemOffsets = { new Vector2(-0.2f, 0.15f), new Vector2(0.2f, 0.15f), new Vector2(0f, 0.45f) };
        readonly List<GameObject> generatedItems = new List<GameObject>();
        // Spawn history, not the number still alive: discarding an item doesn't erase its history.
        readonly Dictionary<GameObject, int> gameSpawnCounts = new Dictionary<GameObject, int>();
        int itemSeed;
        int roundNumber;

        public static ProjectorGame Current { get; private set; }

        public readonly NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>();
        readonly NetworkVariable<bool> buildAvailable = new NetworkVariable<bool>();
        readonly NetworkVariable<int> builderCount = new NetworkVariable<int>();
        NetworkList<ulong> readyPlayers;
        Canvas buildHud;
        Text buildHudText;
        Text confirmLabel;
        Button confirmButton;

        void Awake() => readyPlayers = new NetworkList<ulong>();
        const float SecondPullSeconds = 2.5f;
        const string LeaveHint = "Pull twice to leave";

        float nextPreview;
        // Host, in results: after one pull, play again when this time passes unless a second pull leaves first.
        float playAgainAt = -1f;
        float lastPullAt = float.NegativeInfinity;
        bool isLocallyPaused;
        Canvas pauseOverlay;
        Text pauseOverlayText;

        public bool IsLocallyPaused => isLocallyPaused;

        public override void OnNetworkSpawn()
        {
            roundNumber = 0;
            gameSpawnCounts.Clear();
            buildStarted = false;
            Current = this;
            Phase.OnValueChanged += OnPhaseChanged;
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
            CreatePauseOverlay();
            CreateBuildHud();
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
            if (pauseOverlay != null)
                Destroy(pauseOverlay.gameObject);
            if (buildHud != null)
                Destroy(buildHud.gameObject);
            if (rope != null)
                rope.Pulled -= OnRopePulled;
            if (Current == this)
                Current = null;
        }

        void OnPhaseChanged(GamePhase previous, GamePhase current)
        {
            Apply(current);
            if (current == GamePhase.Results)
                GameAudio.Applause();
        }

        public void SetLocalHeadsetAvailable(bool available)
        {
            var paused = !available;
            Debug.Log($"Headset availability: available={available}, localPaused={isLocallyPaused}, requestedPaused={paused}", this);
            if (paused == isLocallyPaused)
            {
                Debug.Log($"Local game status unchanged: {(isLocallyPaused ? "PAUSE" : "PLAY")}", this);
                return;
            }

            isLocallyPaused = paused;
            Debug.Log($"Local game status: {(paused ? "PAUSE" : "PLAY")}", this);
            ApplyLocalPause();
        }

        void ApplyLocalPause()
        {
            if (hudText != null && isLocallyPaused)
                hudText.text = "PAUSED\nPut your headset on";
            UpdatePauseOverlay(isLocallyPaused);
            UpdateBuildHud();

            var building = Phase.Value == GamePhase.Building;
            foreach (var prop in FindObjectsByType<NetworkProp>(FindObjectsSortMode.None))
                prop.SetGrabAllowed(building && !isLocallyPaused);
            foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                provider.enabled = !isLocallyPaused && Phase.Value != GamePhase.Racing;
        }

        void CreatePauseOverlay()
        {
            var overlayObject = new GameObject("Pause Overlay");
            overlayObject.transform.SetParent(transform, false);
            pauseOverlay = overlayObject.AddComponent<Canvas>();
            pauseOverlay.renderMode = RenderMode.ScreenSpaceOverlay;
            pauseOverlay.sortingOrder = 100;
            overlayObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(overlayObject.transform, false);
            var panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.82f);
            SetFullScreen(panel.rectTransform);

            var textObject = new GameObject("Waiting For Players");
            textObject.transform.SetParent(panelObject.transform, false);
            pauseOverlayText = textObject.AddComponent<Text>();
            pauseOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pauseOverlayText.fontSize = 44;
            pauseOverlayText.color = Color.white;
            pauseOverlayText.alignment = TextAnchor.MiddleCenter;
            pauseOverlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pauseOverlayText.verticalOverflow = VerticalWrapMode.Overflow;
            SetFullScreen(pauseOverlayText.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            overlayObject.SetActive(false);
        }

        // A local, head-relative XR canvas: no collider, prop, or networked scene object is needed.
        void CreateBuildHud()
        {
            var hudObject = new GameObject("Build Confirmation HUD");
            buildHud = hudObject.AddComponent<Canvas>();
            buildHud.renderMode = RenderMode.WorldSpace;
            buildHud.sortingOrder = 10;
            hudObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            hudObject.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 180f);
            hudObject.transform.localScale = Vector3.one * 0.001f;

            var panel = new GameObject("Panel").AddComponent<Image>();
            panel.transform.SetParent(hudObject.transform, false);
            panel.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
            panel.raycastTarget = false;
            SetFullScreen(panel.rectTransform);

            buildHudText = CreateHudLabel("Ready Status", panel.transform);
            SetFullScreen(buildHudText.rectTransform, new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.97f));

            var buttonImage = new GameObject("Confirm Building").AddComponent<Image>();
            buttonImage.transform.SetParent(panel.transform, false);
            buttonImage.color = new Color(0.15f, 0.5f, 0.3f);
            SetFullScreen(buttonImage.rectTransform, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.52f));
            confirmButton = buttonImage.gameObject.AddComponent<Button>();
            confirmButton.targetGraphic = buttonImage;
            confirmButton.onClick.AddListener(ConfirmBuilding);
            confirmLabel = CreateHudLabel("Label", buttonImage.transform);
            SetFullScreen(confirmLabel.rectTransform);
            UpdateBuildHud();
        }

        static Text CreateHudLabel(string name, Transform parent)
        {
            var label = new GameObject(name).AddComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        void UpdateBuildHud()
        {
            if (buildHud == null)
                return;
            if (buildHud.worldCamera == null)
            {
                var origin = FindFirstObjectByType<XROrigin>();
                var camera = origin != null ? origin.Camera : Camera.main;
                if (camera != null)
                {
                    buildHud.worldCamera = camera;
                    buildHud.transform.SetParent(camera.transform, false);
                    // Keep confirmation in the upper view, clear of the central item-placement area.
                    buildHud.transform.localPosition = new Vector3(0f, 0.6f, 1f);
                    buildHud.transform.localRotation = Quaternion.identity;
                }
            }
            buildHud.gameObject.SetActive(Phase.Value == GamePhase.Building && !isLocallyPaused
                && buildHud.worldCamera != null);
            var ready = readyPlayers.Contains(NetworkManager.LocalClientId);
            buildHudText.text = !buildAvailable.Value ? "Waiting for players..."
                : $"{readyPlayers.Count}/{builderCount.Value} players ready";
            confirmLabel.text = ready ? "Confirmed - waiting for others" : "Done building";
            confirmButton.interactable = buildAvailable.Value && !ready && !isLocallyPaused;
        }

        void UpdatePauseOverlay(bool paused)
        {
            if (pauseOverlay == null)
                return;

            pauseOverlay.gameObject.SetActive(paused);
            if (pauseOverlayText != null)
                pauseOverlayText.text = $"Waiting for {LocalPlayerName()} to reconnect";
        }

        static void SetFullScreen(RectTransform rect, Vector2? minimum = null, Vector2? maximum = null)
        {
            rect.anchorMin = minimum ?? Vector2.zero;
            rect.anchorMax = maximum ?? Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        string LocalPlayerName()
        {
            var identity = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None)
                .FirstOrDefault(player => player.IsOwner && player.GetComponent<NetworkAvatar>() != null);
            return identity != null ? identity.Name : "you";
        }

        void HideStaticProps()
        {
            var props = transform.parent != null ? transform.parent.Find("Props") : null;
            if (props == null)
                return;

            props.gameObject.SetActive(false);
        }

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

        public void ConfirmBuilding()
        {
            if (!IsSpawned || isLocallyPaused || Phase.Value != GamePhase.Building
                || !buildAvailable.Value || readyPlayers.Contains(NetworkManager.LocalClientId))
                return;
            GameAudio.Click();
            RequestConfirmBuildingRpc();
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

            UpdateBuildHud();
            switch (Phase.Value)
            {
                case GamePhase.Building:
                    hudText.text = isLocallyPaused ? "PAUSED\nPut your headset on"
                        : !buildAvailable.Value ? "Waiting for players...\n" + LeaveHint
                        : $"Confirm in your HUD when done\n{readyPlayers.Count}/{builderCount.Value} players ready\n{LeaveHint}";
                    if (buildAvailable.Value)
                        GameAudio.PlaySong(GameAudio.Song.BuildPhase);
                    if (Time.time >= nextPreview)
                    {
                        nextPreview = Time.time + PreviewInterval;
                        platforms.ShowPreview(capture.Capture());
                    }
                    if (IsServer)
                        CheckBuildConfirmations();
                    break;
                case GamePhase.Racing:
                    hudText.text = isLocallyPaused ? "PAUSED\nPut your headset on"
                        : (IsServer ? "Pull to end race\n" : "") + LeaveHint + "\n" + race.ClockText();
                    break;
                case GamePhase.Results:
                    if (isLocallyPaused)
                        hudText.text = "PAUSED\nPut your headset on";
                    else if (!IsServer)
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
        void RequestConfirmBuildingRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (Phase.Value != GamePhase.Building || !buildAvailable.Value
                || !NetworkManager.ConnectedClientsIds.Contains(sender) || readyPlayers.Contains(sender))
                return;
            readyPlayers.Add(sender);
        }

        // Evaluate once per server frame, including after joins/disconnects. A departed player must not
        // block the remaining builders; a new player must confirm independently.
        void CheckBuildConfirmations()
        {
            if (!buildAvailable.Value || Phase.Value != GamePhase.Building)
                return;
            var connected = NetworkManager.ConnectedClientsIds;
            builderCount.Value = connected.Count;
            for (var i = readyPlayers.Count - 1; i >= 0; i--)
                if (!connected.Contains(readyPlayers[i]))
                    readyPlayers.RemoveAt(i);
            if (connected.Count > 0 && connected.All(id => readyPlayers.Contains(id)))
                Project();
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
            roundNumber++;
            itemSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var playerCount = NetworkManager.ConnectedClientsIds.Count;
            buildAvailable.Value = false;
            readyPlayers.Clear();
            builderCount.Value = playerCount;
            Debug.Log($"Starting round {roundNumber} with {playerCount} connected player(s) and {itemPrefabs?.Length ?? 0} item prefab reference(s).", this);
            buildStarted = true;
            Phase.Value = GamePhase.Building;
            // Here rather than in Apply: Apply also runs the moment the room spawns, before joiners have loaded.
            GeneratePlayerItems();
            buildAvailable.Value = true;
        }

        void GeneratePlayerItems()
        {
            if (!IsServer)
                return;

            if (itemPrefabs == null || itemPrefabs.Length == 0)
            {
                Debug.LogError("No item mock prefabs are assigned to ProjectorGame.itemPrefabs.", this);
                return;
            }

            var clientIds = NetworkManager.ConnectedClientsIds.OrderBy(id => id).ToArray();
            var playerCount = clientIds.Length;
            var isStarterRound = roundNumber <= Mathf.Clamp(starterRounds, 1, 2);
            var itemsPerPlayer = isStarterRound ? 2 : 1;
            var order = new List<int>(itemPrefabs.Length);
            var seenPrefabs = new HashSet<GameObject>();
            for (var i = 0; i < itemPrefabs.Length; i++)
            {
                var prefab = itemPrefabs[i];
                if (prefab == null || !seenPrefabs.Add(prefab))
                    continue;
                if (!prefab.TryGetComponent<ItemMock>(out var item) || !prefab.TryGetComponent<NetworkObject>(out _))
                {
                    Debug.LogWarning($"Skipping item prefab '{prefab.name}': ItemMock and NetworkObject are required.", this);
                    continue;
                }
                if (item.progression == ItemProgression.Starter ||
                    (!isStarterRound && item.progression == ItemProgression.Advanced))
                    order.Add(i);
            }

            if (order.Count == 0)
            {
                Debug.LogError($"No eligible item prefabs for round {roundNumber}. Assign at least one Starter item with a NetworkObject.", this);
                return;
            }

            var random = new System.Random(itemSeed);
            var roundSpawnCounts = new Dictionary<GameObject, int>();
            var weights = new double[order.Count];

            // Reweight after every successful spawn, across all players. Duplicates remain possible.
            var spawnedThisRound = 0;
            for (var i = 0; i < playerCount; i++)
            {
                var spotX = (PlayerRoster.SlotFor(clientIds[i]) - 1.5f) * 0.8f;
                for (var k = 0; k < itemsPerPlayer; k++)
                {
                    var offset = itemsPerPlayer == 1 ? new Vector2(0f, 0.15f) : ItemOffsets[k];
                    var totalWeight = 0d;
                    for (var candidate = 0; candidate < order.Count; candidate++)
                    {
                        var candidatePrefab = itemPrefabs[order[candidate]];
                        roundSpawnCounts.TryGetValue(candidatePrefab, out var roundCount);
                        gameSpawnCounts.TryGetValue(candidatePrefab, out var gameCount);
                        weights[candidate] = SpawnWeight(roundCount, gameCount);
                        totalWeight += weights[candidate];
                    }
                    var roll = random.NextDouble() * totalWeight;
                    var selected = order.Count - 1;
                    for (var candidate = 0; candidate < order.Count; candidate++)
                    {
                        roll -= weights[candidate];
                        if (roll < 0d)
                        {
                            selected = candidate;
                            break;
                        }
                    }
                    var prefabIndex = order[selected];
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
                    networkObject.DontDestroyWithOwner = true;
                    networkObject.SpawnWithOwnership(clientIds[i], true);
                    roundSpawnCounts.TryGetValue(prefab, out var roundSpawns);
                    gameSpawnCounts.TryGetValue(prefab, out var gameSpawns);
                    roundSpawnCounts[prefab] = roundSpawns + 1;
                    gameSpawnCounts[prefab] = gameSpawns + 1;
                    spawnedThisRound++;
                }
            }
            Debug.Log($"Generated {spawnedThisRound} item(s) for {playerCount} player(s).", this);
        }

        // One previous spawn this round reduces weight 101-fold; one in the game reduces it fourfold.
        // Rational decay stays positive even in long games, unlike exponential weights that can underflow.
        static double SpawnWeight(int roundCount, int gameCount)
            => 1d / ((1d + 100d * roundCount * roundCount) * (1d + 3d * gameCount));

        void ClearGeneratedItems(bool preserveCommitted = false)
        {
            if (!IsServer)
            {
                generatedItems.Clear();
                return;
            }

            for (var i = generatedItems.Count - 1; i >= 0; i--)
            {
                if (generatedItems[i] == null)
                {
                    generatedItems.RemoveAt(i);
                    continue;
                }
                if (preserveCommitted && generatedItems[i].TryGetComponent<NetworkProp>(out var prop) && prop.Committed.Value)
                    continue;
                var networkObject = generatedItems[i].GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                    networkObject.Despawn(true);
                else
                    Destroy(generatedItems[i]);
                generatedItems.RemoveAt(i);
            }
        }

        // Host: once all connected players confirm, freeze the projection and start the race.
        // Everyone draws the level from the same prop poses, the host's, so all players race on the same shapes.
        void Project()
        {
            if (!IsServer || Phase.Value != GamePhase.Building)
                return;

            var props = new List<Transform>();
            foreach (var prop in capture.PropsInBeam())
            {
                if (!prop.TryGetComponent<NetworkProp>(out var networkProp) || !networkProp.IsSpawned)
                    continue;
                if (networkProp.Committed.Value)
                {
                    props.Add(prop);
                    continue;
                }
                // Partially visible items are discarded, even if clipping could produce a collider.
                if (!capture.IsFullyInBeam(prop))
                    continue;
                var projected = capture.Capture(new[] { prop }, null, true);
                if (projected.Count == 0 || networkProp.OverlapsAnotherProp())
                    continue;
                var item = projected[0];
                var usable = item.Kind == ShapeKind.Coin
                    ? item.Center.x >= capture.LevelMin.x && item.Center.x <= capture.LevelMax.x &&
                      item.Center.y >= capture.LevelMin.y && item.Center.y <= capture.LevelMax.y
                    : item.Parts.Count > 0;
                if (usable)
                    props.Add(prop);
            }
            if (props.Count == 0 && !evenIfEmpty)
                return false;

            foreach (var prop in props)
                prop.GetComponent<NetworkProp>().CommitPlacement();
            ClearGeneratedItems(preserveCommitted: true);

            var ids = new ulong[props.Count];
            var positions = new Vector3[props.Count];
            var rotations = new Quaternion[props.Count];
            for (var i = 0; i < props.Count; i++)
            {
                ids[i] = props[i].GetComponent<NetworkObject>().NetworkObjectId;
                props[i].GetPositionAndRotation(out positions[i], out rotations[i]);
            }
            BuildLevelRpc(ids, positions, rotations);
            buildAvailable.Value = false;
            Phase.Value = GamePhase.Racing;
            race.BeginRace();
        }

        [Rpc(SendTo.Everyone)]
        void BuildLevelRpc(ulong[] ids, Vector3[] positions, Quaternion[] rotations)
        {
            var props = new List<Transform>();
            var poses = new List<Pose>();
            for (var i = 0; i < ids.Length; i++)
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ids[i], out var networkObject))
                {
                    props.Add(networkObject.transform);
                    poses.Add(new Pose(positions[i], rotations[i]));
                }
            platforms.BuildLevel(capture.Capture(props, poses, true));
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
            UpdateBuildHud();
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
                prop.SetGrabAllowed(building && !isLocallyPaused);
            foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                provider.enabled = !racing && !isLocallyPaused;

            if (racing)
                FaceTheWall();
            if (!building)
                GameAudio.PlaySong(racing ? GameAudio.Song.Race : GameAudio.Song.None);
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
