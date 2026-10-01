using System.Collections;
using Projector.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Gameplay
{
    // The projector room's timer. Players arrange props in the beam; when time runs out (or anyone presses
    // the projector's first button / PROJECT NOW) the host captures the shadows, sends the outlines to
    // everyone, and moves the game to the 2D race.
    public class BuildPhase : NetworkBehaviour
    {
        [SerializeField] float durationSeconds = 90f;
        [SerializeField] ShadowCaster shadowCaster;
        [SerializeField] Text timerText;
        [SerializeField] XRBaseInteractable projectorButton;

        readonly NetworkVariable<double> endTime = new NetworkVariable<double>();
        bool projecting;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                endTime.Value = NetworkManager.ServerTime.Time + durationSeconds;
            if (projectorButton != null)
                projectorButton.selectEntered.AddListener(OnProjectorButton);
        }

        public override void OnNetworkDespawn()
        {
            if (projectorButton != null)
                projectorButton.selectEntered.RemoveListener(OnProjectorButton);
        }

        void OnProjectorButton(SelectEnterEventArgs args) => ProjectNow();

        public void ProjectNow()
        {
            if (IsSpawned)
                RequestProjectRpc();
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            var remaining = Mathf.Max(0f, (float)(endTime.Value - NetworkManager.ServerTime.Time));
            timerText.text = projecting
                ? "PROJECTING..."
                : $"Hang objects in the projector beam\n{(int)remaining / 60}:{(int)remaining % 60:00}";

            if (IsServer && remaining <= 0f)
                BeginProjection();
        }

        [Rpc(SendTo.Server)]
        void RequestProjectRpc() => BeginProjection();

        void BeginProjection()
        {
            if (projecting)
                return;

            projecting = true;
            ShadowLevel.Pack(shadowCaster.Capture(), out var points, out var counts);
            ReceiveLevelRpc(points, counts);
            StartCoroutine(StartRaceShortly());
        }

        [Rpc(SendTo.Everyone)]
        void ReceiveLevelRpc(Vector2[] points, int[] counts)
        {
            projecting = true;
            ShadowLevel.Set(ShadowLevel.Unpack(points, counts));
        }

        // Gives the level RPC time to reach every client ahead of the scene change.
        IEnumerator StartRaceShortly()
        {
            yield return new WaitForSeconds(1.5f);
            GameScenes.LoadForEveryone(GameScenes.Platformer2D);
        }
    }
}
