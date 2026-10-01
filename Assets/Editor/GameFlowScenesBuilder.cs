using UnityEditor;

namespace Projector.Editor
{
    // Rebuilds the generated scenes in the game flow except the Projector Room, which needs the local food pack.
    public static class GameFlowScenesBuilder
    {
        [MenuItem("Projector/Build Menu and 2D Scenes")]
        public static void BuildAll()
        {
            Platformer2DSceneBuilder.BuildScene();
            SceneBuildUtility.RemoveSceneFromBuildSettings("Assets/Scenes/Lobby.unity");
            SceneBuildUtility.AddSceneToBuildSettings("Assets/Scenes/LandingMenu.unity", first: true);
            // Last, so the editor is left in the first scene of the game.
            LandingMenuBuilder.RebuildLandingMenuScene();
        }
    }
}
