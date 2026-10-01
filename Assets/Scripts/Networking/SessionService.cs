using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Projector.Networking
{
    // One multiplayer session at a time, backed by Unity Multiplayer Services (Lobby + Relay).
    // Creating or joining a relay session also starts Netcode as host or client on NetworkManager.Singleton,
    // so scene loads issued by the host through GameScenes reach every connected player.
    public static class SessionService
    {
        public const int MaxPlayers = 4;

        public static ISession Current { get; private set; }

        // Raised when the current session starts, ends, or its player list changes.
        public static event Action SessionChanged;

        public static bool IsHost => Current == null || Current.IsHost;

        // Private sessions are join-by-code only; public ones can also be found by QuickJoinAsync.
        public static async Task<ISession> HostAsync(bool isPrivate = true)
        {
            await PrepareAsync();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = isPrivate }
                .WithPlayerName()
                .WithRelayNetwork();
            Attach(await MultiplayerService.Instance.CreateSessionAsync(options));
            KeepClientScenesOnJoin();
            return Current;
        }

        public static async Task<ISession> JoinAsync(string code)
        {
            await PrepareAsync();
            var options = new JoinSessionOptions().WithPlayerName();
            Attach(await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), options));
            return Current;
        }

        // Joins any open public session, or hosts a new public one when there's none to join.
        public static async Task<ISession> QuickJoinAsync()
        {
            await PrepareAsync();
            var quickJoin = new QuickJoinOptions { CreateSession = true, Timeout = TimeSpan.FromSeconds(5) };
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = false }
                .WithPlayerName()
                .WithRelayNetwork();
            Attach(await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, options));
            if (Current.IsHost)
                KeepClientScenesOnJoin();
            return Current;
        }

        // Disconnects right away, then tells the session service (which can be slow or fail on a bad network;
        // nothing waits on it to get the player back to the menu).
        public static async Task LeaveAsync()
        {
            var session = Current;
            Detach();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            try
            {
                // A departing host would hand the session to someone whose game has no server; close it instead
                // so everyone else is sent back to the menu.
                if (session != null && session.IsHost)
                    await session.AsHost().DeleteAsync();
                else if (session != null)
                    await session.LeaveAsync();
            }
            catch (Exception exception)
            {
                // The session may already be gone (host left first); there's nothing left to leave.
                Debug.LogWarning($"Leaving session failed: {exception.Message}");
            }
        }

        // Players join from the menu scene the host is also in. Netcode would normally reload it on the joining
        // client (resetting its menus); additive client sync keeps the already-loaded scene instead.
        static void KeepClientScenesOnJoin()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer && manager.SceneManager != null)
                manager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
        }

        public static string GetDisplayName(IReadOnlyPlayer player, int index)
        {
            var name = player.GetPlayerName();
            return string.IsNullOrEmpty(name) ? $"Player {index + 1}" : name;
        }

        static async Task PrepareAsync()
        {
            if (Current != null)
                await LeaveAsync();

            EnsureNetworkManager();

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // Generates a random "Name#1234" on first sign-in so WithPlayerName has something to publish.
            if (string.IsNullOrEmpty(AuthenticationService.Instance.PlayerName))
                await AuthenticationService.Instance.GetPlayerNameAsync();
        }

        static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
                return;

            var managerObject = new GameObject("Network Manager");
            UnityEngine.Object.DontDestroyOnLoad(managerObject);
            var transport = managerObject.AddComponent<UnityTransport>();
            var manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.ConnectionApproval = false;

            // Losing the connection to the host (crash, network drop) ends the game for this player too.
            manager.OnClientStopped += wasHost =>
            {
                if (!wasHost && Current != null)
                    ReturnToMenuAfterDisconnect();
            };
        }

        static void Attach(ISession session)
        {
            Current = session;
            session.Changed += RaiseSessionChanged;
            session.PlayerJoined += OnPlayerListChanged;
            session.PlayerHasLeft += OnPlayerListChanged;
            session.RemovedFromSession += OnSessionEnded;
            session.Deleted += OnSessionEnded;
            RaiseSessionChanged();
        }

        static void Detach()
        {
            if (Current == null)
                return;

            Current.Changed -= RaiseSessionChanged;
            Current.PlayerJoined -= OnPlayerListChanged;
            Current.PlayerHasLeft -= OnPlayerListChanged;
            Current.RemovedFromSession -= OnSessionEnded;
            Current.Deleted -= OnSessionEnded;
            Current = null;
            RaiseSessionChanged();
        }

        static void OnPlayerListChanged(string playerId) => RaiseSessionChanged();

        // The host closed the session or kicked us: drop the connection and send this player back to the menus.
        static void OnSessionEnded()
        {
            Detach();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            if (SceneManager.GetActiveScene().name != GameScenes.LandingMenu)
                SceneManager.LoadScene(GameScenes.LandingMenu);
        }

        static async void ReturnToMenuAfterDisconnect()
        {
            var leaving = LeaveAsync();
            if (SceneManager.GetActiveScene().name != GameScenes.LandingMenu)
                SceneManager.LoadScene(GameScenes.LandingMenu);
            await leaving;
        }

        static void RaiseSessionChanged() => SessionChanged?.Invoke();
    }
}
