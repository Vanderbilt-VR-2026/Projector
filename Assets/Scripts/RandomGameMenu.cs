using Projector.Networking;
using UnityEngine;

// JOIN RANDOM GAME: joins any open public game, or hosts a new public one if there isn't one.
public class RandomGameMenu : MonoBehaviour
{
    [SerializeField] LandingMenu landingMenu;

    public void BeginSearch()
    {
        landingMenu.RunSessionTask("Looking for a game...", SessionService.QuickJoinAsync, "Could not find a game");
    }
}
