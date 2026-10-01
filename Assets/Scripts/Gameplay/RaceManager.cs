using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Projector.Gameplay
{
    // Runs the 2D race on the host: countdown, clock, finish order, and scoring. When everyone has finished
    // (or time runs out) it sends the results to every player's scoreboard.
    // Points: 1000 / 750 / 500 / 250 by finish place, +100 per coin, -50 per fall, never below 0.
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

        [SerializeField] float countdownSeconds = 3f;
        [SerializeField] float timeLimitSeconds = 90f;
        [SerializeField] float finishX = 7.2f;
        [SerializeField] float pitHeight = 1f;
        [SerializeField] Text hudText;
        [SerializeField] Scoreboard scoreboard;

        public static RaceManager Current { get; private set; }

        readonly NetworkVariable<double> startTime = new NetworkVariable<double>(-1);
        readonly NetworkVariable<bool> over = new NetworkVariable<bool>();
        readonly Dictionary<ulong, Tally> tallies = new Dictionary<ulong, Tally>();

        public float FinishX => finishX;
        public float PitHeight => pitHeight;
        public bool IsRunning => startTime.Value > 0 && Now >= startTime.Value && !over.Value;
        float Elapsed => (float)(Now - startTime.Value);
        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Current = this;

            // The thumbstick drives the runner here, so the rig itself stays put.
            foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
                provider.enabled = false;

            if (IsServer)
                StartCoroutine(StartWhenRunnersArrive());
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this)
                Current = null;
        }

        IEnumerator StartWhenRunnersArrive()
        {
            var waited = 0f;
            while (Runners().Count < NetworkManager.ConnectedClientsIds.Count && waited < 25f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            startTime.Value = Now + countdownSeconds;
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            if (over.Value)
                hudText.text = "RACE OVER";
            else if (startTime.Value < 0)
                hudText.text = "GET READY";
            else if (Now < startTime.Value)
                hudText.text = Mathf.CeilToInt((float)(startTime.Value - Now)).ToString();
            else
            {
                var remaining = Mathf.Max(0f, timeLimitSeconds - Elapsed);
                hudText.text = Elapsed < 1f ? "GO!" : $"{(int)remaining / 60}:{(int)remaining % 60:00}";
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
            ShowResultsRpc(results.ToArray());
        }

        [Rpc(SendTo.Everyone)]
        void ShowResultsRpc(Result[] results)
        {
            scoreboard.Show(results.Select(result => new Scoreboard.Entry
            {
                playerName = result.Name.ToString(),
                points = result.Points,
                finishSeconds = result.Seconds
            }));
        }
    }
}
