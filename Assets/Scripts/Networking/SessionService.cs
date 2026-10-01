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

        public static async Task<ISession> HostAsync()
        {
            await PrepareAsync();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }
                .WithPlayerName()
                .WithRelayNetwork();
            Attach(await MultiplayerService.Instance.CreateSessionAsync(options));
            return Current;
        }

        public static async Task<ISession> JoinAsync(string code)
        {
            await PrepareAsync();
            var options = new JoinSessionOptions().WithPlayerName();
            Attach(await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), options));
            return Current;
        }

        public static async Task LeaveAsync()
        {
            var session = Current;
            Detach();

            try
            {
                if (session != null)
                    await session.LeaveAsync();
            }
            catch (SessionException exception)
            {
                Debug.LogWarning($"Leaving session failed: {exception.Message}");
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
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

        static void RaiseSessionChanged() => SessionChanged?.Invoke();
    }
}
