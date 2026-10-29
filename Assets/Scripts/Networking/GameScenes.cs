using Projector.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Projector.Networking
{
    // The game's scene flow: Landing Menu (menus, including the pre-game screen) -> Projector Room, where
    // building and the projected 2D race both happen.
    // Every scene here must be listed in the build settings so Netcode can sync it to clients.
    public static class GameScenes
    {
        public const string LandingMenu = "LandingMenu";
        public const string ProjectorRoom = "ProjectorRoom";

        // On the host this moves every connected player; offline (scene opened directly in the editor) it loads locally.
        public static void LoadForEveryone(string sceneName)
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                if (!manager.IsServer)
                {
                    Debug.LogWarning($"Only the host can change scenes (tried to load {sceneName}).");
                    return;
                }

                manager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        static bool returning;

        // Back to the menu: in the theater the curtains close first, then the session is closed in the background
        // while the menu loads. Leaving by choice, being sent out by the host and losing the connection all end here.
        public static async void ReturnToLanding()
        {
            if (returning)
                return;
            returning = true;
            try
            {
                if (TheaterCurtains.Current != null)
                    await TheaterCurtains.Current.CloseAsync();
                var leaving = SessionService.LeaveAsync();
                if (SceneManager.GetActiveScene().name != LandingMenu)
                    SceneManager.LoadScene(LandingMenu, LoadSceneMode.Single);
                await leaving;
            }
            finally
            {
                returning = false;
            }
        }
    }
}
