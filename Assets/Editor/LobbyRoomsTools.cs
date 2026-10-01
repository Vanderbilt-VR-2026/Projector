using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Projector > Lobby Rooms > Capture Preview Shots: renders PNG previews of Room1 and Lobby into LobbyPreviews/ (project root, not committed) for PR descriptions.
/// Projector > Lobby Rooms > Build And Run Lobby Test: builds just Room1 + Lobby to Builds/LobbyTest.apk and launches it on the connected Quest.
/// </summary>
public static class LobbyRoomsTools
{
    const string PreviewDir = "LobbyPreviews";

    [MenuItem("Projector/Lobby Rooms/Capture Preview Shots")]
    public static void CapturePreviews()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Directory.CreateDirectory(PreviewDir);

        EditorSceneManager.OpenScene("Assets/Scenes/Room1.unity");
        Shot("room1_spawn_view", new Vector3(0f, 1.6f, -1.5f), Quaternion.Euler(5f, 0f, 0f), 80f);
        Shot("room1_overview", new Vector3(-3.3f, 3.1f, -3.5f), Quaternion.Euler(28f, 40f, 0f), 85f);

        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
        Shot("lobby_spawn_view", new Vector3(0f, 1.6f, -4.4f), Quaternion.Euler(6f, 0f, 0f), 85f);
        Shot("lobby_overview", new Vector3(-4.3f, 3.6f, -5.5f), Quaternion.Euler(28f, 38f, 0f), 85f);
        Shot("lobby_shadow_preview", new Vector3(1.6f, 1.7f, -1.4f), Quaternion.Euler(8f, 60f, 0f), 75f);
        Shot("lobby_item_sandbox", new Vector3(-2.6f, 1.6f, 0.6f), Quaternion.Euler(30f, -90f, 0f), 80f);

        Debug.Log("[Lobby] Preview shots written to " + Path.GetFullPath(PreviewDir));
    }

    [MenuItem("Projector/Lobby Rooms/Build And Run Lobby Test")]
    public static void BuildAndRunLobbyTest()
    {
        Directory.CreateDirectory("Builds");
        string resultFile = "Builds/lobby_build_result.txt";
        if (File.Exists(resultFile)) File.Delete(resultFile);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Room1.unity", "Assets/Scenes/Lobby.unity" },
            locationPathName = "Builds/LobbyTest.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.AutoRunPlayer,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);

        var sb = new StringBuilder();
        sb.AppendLine($"{report.summary.result} | errors {report.summary.totalErrors} | warnings {report.summary.totalWarnings} | {report.summary.totalTime}");
        foreach (var step in report.steps)
            foreach (var msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    sb.AppendLine("ERROR: " + msg.content);
        File.WriteAllText(resultFile, sb.ToString());
        Debug.Log("[Lobby] " + sb);
    }

    static void Shot(string name, Vector3 pos, Quaternion rot, float fov)
    {
        var go = new GameObject("PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.02f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes($"{PreviewDir}/{name}.png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
