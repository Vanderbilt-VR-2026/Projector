using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Projector.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Projector.Gameplay
{
    // Runs the race on the projection stage, on the host: spawns a runner per player, counts down, keeps the
    // clock, records finishes, coins and falls, and hands results to the game when everyone is done or time
    // runs out. Points: 1000 / 750 / 500 / 250 by finish place, +100 per coin, -50 per fall, never below 0.
    public class RaceManager : NetworkBehaviour
    {
        static readonly int[] PlacePoints = { 1000, 750, 500, 250 };
        const int CoinPoints = 100;
        const int FallPenalty = 50;

        public struct Result : INetworkSerializable
        {
            public FixedString64Bytes Name;
            public int Points;
            public float Seconds;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Name);
                serializer.SerializeValue(ref Points);
                serializer.SerializeValue(ref Seconds);
            }
        }

        class Tally
        {
            public int Coins;
            public int Falls;
            public float? FinishSeconds;
        }

        [SerializeField] NetworkObject runnerPrefab;
        [Tooltip("The projection stage: level coordinates are this transform's local x/y.")]
        [SerializeField] Transform stage;
        [SerializeField] ProjectorGame game;
        [Tooltip("Big countdown text drawn on the projection.")]
        [SerializeField] Text banner;
        [SerializeField] float countdownSeconds = 3f;
        [SerializeField] float timeLimitSeconds = 90f;
        [SerializeField] float startX = -8.5f;
        [SerializeField] float finishX = 7.2f;
        [SerializeField] float pitHeight = 1f;

        public static RaceManager Current { get; private set; }

        readonly NetworkVariable<double> startTime = new NetworkVariable<double>(-1);
        readonly NetworkVariable<bool> over = new NetworkVariable<bool>();
        readonly Dictionary<ulong, Tally> tallies = new Dictionary<ulong, Tally>();

        public float FinishX => finishX;
        public float PitHeight => pitHeight;
        public bool IsRunning => game.Phase.Value == GamePhase.Racing && startTime.Value > 0 && Now >= startTime.Value && !over.Value;
        float Elapsed => (float)(Now - startTime.Value);
        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this)
                Current = null;
        }

        // Each slot lines up side by side on the start platform.
        public Vector3 StartPoint(int slot) => stage.TransformPoint(new Vector3(startX + (slot - 1.5f) * 0.6f, 2.65f, 0f));

        public Vector2 ToLevel(Vector3 world) => stage.InverseTransformPoint(world);

        public string ClockText()
        {
            if (startTime.Value < 0 || Now < startTime.Value)
                return "Get ready...";
            if (over.Value)
                return "Finished!";
            var remaining = Mathf.Max(0f, timeLimitSeconds - Elapsed);
            return $"Race to the gold platform!\n{(int)remaining / 60}:{(int)remaining % 60:00}";
        }

        // Host only.
        public void BeginRace()
        {
            tallies.Clear();
            over.Value = false;
            startTime.Value = -1;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                var slot = PlayerRoster.SlotFor(clientId);
                var runner = Instantiate(runnerPrefab, StartPoint(slot), Quaternion.identity);
                runner.GetComponent<PlayerIdentity>().Slot.Value = slot;
                runner.SpawnWithOwnership(clientId, destroyWithScene: true);
            }
            StartCoroutine(CountdownWhenRunnersArrive());
        }

        // Host only.
        public void ResetRace()
        {
            StopAllCoroutines();
            foreach (var runner in Runners())
                runner.NetworkObject.Despawn(true);
            tallies.Clear();
            startTime.Value = -1;
            over.Value = false;
        }

        // Host only: stop the race now; anyone who hasn't finished is a DNF. Ignored during the countdown.
        public void EndNow()
        {
            if (!IsRunning)
                return;
            StopAllCoroutines();
            EndRace();
        }

        IEnumerator CountdownWhenRunnersArrive()
        {
            // Spawns reach clients within a round trip; a short beat keeps the countdown fair for everyone.
            yield return new WaitForSeconds(0.5f);
            startTime.Value = Now + countdownSeconds;
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            if (banner != null)
            {
                var racing = game.Phase.Value == GamePhase.Racing;
                if (!racing || startTime.Value < 0 || over.Value)
                    banner.text = "";
                else if (Now < startTime.Value)
                    banner.text = Mathf.CeilToInt((float)(startTime.Value - Now)).ToString();
                else
                    banner.text = Elapsed < 1f ? "GO!" : "";
            }

            if (IsServer && IsRunning && (Elapsed >= timeLimitSeconds || EveryoneFinished()))
                EndRace();
        }

        [Rpc(SendTo.Server)]
        public void ReportCoinRpc(RpcParams rpcParams = default) => TallyFor(rpcParams.Receive.SenderClientId).Coins++;

        [Rpc(SendTo.Server)]
        public void ReportFallRpc(RpcParams rpcParams = default) => TallyFor(rpcParams.Receive.SenderClientId).Falls++;

        [Rpc(SendTo.Server)]
        public void ReportFinishRpc(RpcParams rpcParams = default)
        {
            var tally = TallyFor(rpcParams.Receive.SenderClientId);
            if (IsRunning && tally.FinishSeconds == null)
                tally.FinishSeconds = Elapsed;
        }

        Tally TallyFor(ulong clientId)
        {
            if (!tallies.TryGetValue(clientId, out var tally))
                tallies[clientId] = tally = new Tally();
            return tally;
        }

        List<RunnerController> Runners() => FindObjectsByType<RunnerController>(FindObjectsSortMode.None).ToList();

        bool EveryoneFinished()
        {
            var runners = Runners();
            return runners.Count > 0 && runners.All(runner => TallyFor(runner.OwnerClientId).FinishSeconds != null);
        }

        void EndRace()
        {
            over.Value = true;

            var finishOrder = tallies.Where(pair => pair.Value.FinishSeconds != null)
                .OrderBy(pair => pair.Value.FinishSeconds.Value).Select(pair => pair.Key).ToList();
            var results = new List<Result>();
            foreach (var runner in Runners())
            {
                var tally = TallyFor(runner.OwnerClientId);
                var place = finishOrder.IndexOf(runner.OwnerClientId);
                var points = (place >= 0 && place < PlacePoints.Length ? PlacePoints[place] : 0)
                             + tally.Coins * CoinPoints - tally.Falls * FallPenalty;
                results.Add(new Result
                {
                    Name = runner.GetComponent<PlayerIdentity>().Name,
                    Points = Mathf.Max(0, points),
                    Seconds = tally.FinishSeconds ?? -1f
                });
            }
            game.ShowResults(results.ToArray());
        }
    }
}
