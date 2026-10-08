using UnityEditor;

namespace Projector.Editor
{
    // Rebuilds the network prefabs and the generated scenes in the game flow. The projector room itself isn't
    // rebuilt; its gameplay objects are re-added in place.
    public static class GameFlowScenesBuilder
    {
        [MenuItem("Projector/Build All Game Scenes")]
        public static void BuildAll()
        {
            NetworkPrefabsBuilder.BuildAll();
            ProjectorRoomGameplayBuilder.AddGameplay();
            // The waiting room is the landing menu's game-found screen, and the race plays on the projector wall.
            SceneBuildUtility.RemoveSceneFromBuildSettings("Assets/Scenes/Lobby.unity");
            SceneBuildUtility.RemoveSceneFromBuildSettings("Assets/Scenes/Platformer2D.unity");
            SceneBuildUtility.AddSceneToBuildSettings("Assets/Scenes/LandingMenu.unity", first: true);
            // Last, so the editor is left in the first scene of the game.
            LandingMenuBuilder.RebuildLandingMenuScene();
        }
    }
}
