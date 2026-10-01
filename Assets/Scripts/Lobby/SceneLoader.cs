using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectorGame.Lobby
{
    /// <summary>
    /// Small helper so buttons can move between rooms. Only loads scenes that are in the build,
    /// so a missing scene logs a warning instead of breaking the app.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public string sceneName;

        public void Load()
        {
            TryLoad(sceneName);
        }

        public void Load(string scene)
        {
            TryLoad(scene);
        }

        public static bool TryLoad(string scene)
        {
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"[Lobby] Scene '{scene}' is not in the build yet.");
                return false;
            }
            SceneManager.LoadScene(scene);
            return true;
        }
    }
}
