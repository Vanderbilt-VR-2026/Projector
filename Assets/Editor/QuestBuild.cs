using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Projector.Editor
{
    // One-click headset build: Builds/Projector.apk, ready to sideload onto a Quest with
    // `adb install -r Builds/Projector.apk`.
    public static class QuestBuild
    {
        public const string OutputPath = "Builds/Projector.apk";

        [MenuItem("Projector/Build Quest APK")]
        public static void Build()
        {
            ApplyQuestSettings();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, scene => scene.path),
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android
            });
            Debug.Log($"Quest build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB at {OutputPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // Quest 3 needs ARM64 + IL2CPP, Android 12L (API 32) or newer for Meta's OpenXR runtime, and network
        // access for Lobby/Relay.
        static void ApplyQuestSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.vanderbiltvr.projector");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.forceInternetPermission = true;
            AssetDatabase.SaveAssets();
        }
    }
}
