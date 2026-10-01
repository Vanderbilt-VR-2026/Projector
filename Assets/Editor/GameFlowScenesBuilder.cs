using UnityEditor;

namespace Projector.Editor
{
    // Rebuilds every scene in the multiplayer flow except the Projector Room, which needs the local food pack.
    public static class GameFlowScenesBuilder
    {
        [MenuItem("Projector/Build Menu, Lobby and 2D Scenes")]
        public static void BuildAll()
        {
            LobbySceneBuilder.BuildScene();
            Platformer2DSceneBuilder.BuildScene();
            // Last, so the editor is left in the first scene of the game.
            LandingMenuBuilder.CreateLandingMenuScene();
        }
    }
}
