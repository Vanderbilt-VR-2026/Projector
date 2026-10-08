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

        // Back to the menu at once; the session is closed in the background.
        public static async void ReturnToLanding()
        {
            var leaving = SessionService.LeaveAsync();
            SceneManager.LoadScene(LandingMenu, LoadSceneMode.Single);
            await leaving;
        }
    }
}
