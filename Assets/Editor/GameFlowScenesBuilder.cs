using UnityEditor;

namespace Projector.Editor
{
    // Rebuilds the network prefabs and every scene in the game flow. The projector room itself isn't
    // rebuilt (that needs the local food pack); its gameplay objects are re-added in place.
    public static class GameFlowScenesBuilder
    {
        [MenuItem("Projector/Build All Game Scenes")]
        public static void BuildAll()
        {
            NetworkPrefabsBuilder.BuildAll();
            LobbySceneBuilder.BuildScene();
            Platformer2DSceneBuilder.BuildScene();
            ProjectorRoomGameplayBuilder.AddGameplay();
            // Last, so the editor is left in the first scene of the game.
            LandingMenuBuilder.CreateLandingMenuScene();
        }
    }
}
