using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Projector.Networking
{
    // Per-scene: spawns one `prefab` per player, owned by that player, at their slot's spawn point.
    // Each peer asks for its own object once the scene's networked objects exist, which covers both
    // scene loads and players joining a lobby late. Spawned objects are destroyed with the scene.
    // On a scene change the host holds every request until all clients have loaded the scene: objects
    // spawned while a client is still loading never reach that client.
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] NetworkObject prefab;
        [SerializeField] Transform[] spawnPoints;
        [Tooltip("Where this player's XR rig stands, per slot. Leave empty to use the spawn points.")]
        [SerializeField] Transform[] rigPoints;

        public static PlayerSpawner Current { get; private set; }

        const float LoadTimeoutSeconds = 20f;

        readonly HashSet<ulong> spawnedClients = new HashSet<ulong>();
        readonly List<ulong> pendingClients = new List<ulong>();
        bool everyoneLoaded;

        public override void OnNetworkSpawn()
        {
            Current = this;
            if (IsServer)
            {
                // Alone (or opened directly) there is nobody to wait for.
                everyoneLoaded = NetworkManager.ConnectedClientsIds.Count <= 1;
                if (!everyoneLoaded)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
                    StartCoroutine(StopWaitingAfterTimeout());
                }
            }
            RequestSpawnRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this)
                Current = null;
            if (IsServer && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == gameObject.scene.name)
                SpawnPending();
        }

        // A client that never reports in shouldn't keep everyone else invisible.
        IEnumerator StopWaitingAfterTimeout()
        {
            yield return new WaitForSeconds(LoadTimeoutSeconds);
            SpawnPending();
        }

        void SpawnPending()
        {
            if (everyoneLoaded)
                return;

            everyoneLoaded = true;
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            foreach (var clientId in pendingClients)
                SpawnFor(clientId);
            pendingClients.Clear();
        }

        public Transform SpawnPoint(int slot) => spawnPoints[slot % spawnPoints.Length];

        // Moves the local XR rig to its slot so players in the same scene don't stand inside each other.
        public void PlaceLocalRig(int slot)
        {
            var points = rigPoints != null && rigPoints.Length > 0 ? rigPoints : spawnPoints;
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || points.Length == 0)
                return;

            var point = points[slot % points.Length];
            origin.transform.SetPositionAndRotation(point.position, point.rotation);
        }

        [Rpc(SendTo.Server)]
        void RequestSpawnRpc(RpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (everyoneLoaded)
                SpawnFor(clientId);
            else if (!pendingClients.Contains(clientId))
                pendingClients.Add(clientId);
        }

        void SpawnFor(ulong clientId)
        {
            if (!spawnedClients.Add(clientId) || !NetworkManager.ConnectedClients.ContainsKey(clientId))
                return;

            var slot = PlayerRoster.SlotFor(clientId);
            var point = SpawnPoint(slot);
            var instance = Instantiate(prefab, point.position, point.rotation);
            if (instance.TryGetComponent(out Gameplay.PlayerIdentity identity))
                identity.Slot.Value = slot;
            instance.SpawnWithOwnership(clientId, destroyWithScene: true);
        }
    }
}
