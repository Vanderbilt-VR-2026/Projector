using Unity.Netcode;
using UnityEngine;

namespace Projector.Networking
{
    // Lets a gameplay scene be opened and played on its own (editor, or a headset with no session):
    // if nothing is hosting yet, start a local host so the scene's networked objects spawn.
    public class OfflineHost : MonoBehaviour
    {
        void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return;

            SessionService.EnsureNetworkManager();
            NetworkManager.Singleton.StartHost();
        }
    }
}
