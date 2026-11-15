using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Projector.Editor
{
    // Bakes the projector room's lighting so the Quest pays for none of it at runtime: the projector's spot is
    // stored in lightmaps on the scenery that never moves (the room's shell, the table, the projector), and in a
    // grid of light probes for everything that does (props, avatars, curtains). The stage wash stays Mixed, so it
    // still lights moving things directly as the main light while its bounce is baked. The Quest URP asset runs
    // with Additional Lights disabled, so the spot has no realtime cost.
    // Run after ProjectorRoomGameplayBuilder.AddGameplay, which re-creates the spot.
    public static class ProjectorRoomLightingBuilder
    {
        private const string ScenePath = "Assets/Scenes/ProjectorRoom.unity";
        private const string SettingsPath = "Assets/Generated/ProjectorRoom.lighting";
        // Roots whose meshes never move. The curtains open and close, and seats are unlit with their shading in
        // vertex colors, so neither is here.
        private static readonly string[] StaticRoots = { "Room", "Long Table", "Projector" };
        // Lightmap texels per unit: the room is large and dim, so most of it needs few; what players see up close
        // gets more.
        private const float TexelsPerUnit = 4f;
        private static readonly Dictionary<string, float> CloseUpScale = new Dictionary<string, float>
        {
            { "Long Table", 4f },
            { "Projector", 4f },
            { "Stage", 2f },
            { "Front Wall", 2f },
        };

        [MenuItem("Projector/Bake Projector Room Lighting")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyLightingSettings();
            int staticCount = MarkStaticScenery();
            SetLightModes();
            CreateLightProbes();

            if (!Lightmapping.Bake())
                throw new System.InvalidOperationException("Lightmap bake failed for " + ScenePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked lighting for {ScenePath}: {staticCount} static renderers, {LightmapSettings.lightmaps.Length} lightmap(s).");
        }

        public static void BakeFromCommandLine()
        {
            Bake();
        }

        private static void ApplyLightingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings == null)
            {
                SceneBuildUtility.EnsureFolder("Assets/Generated");
                settings = new LightingSettings { name = "ProjectorRoom" };
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = TexelsPerUnit;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 1024;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.ao = false;
            settings.maxBounces = 2;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 64;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
        }

        private static int MarkStaticScenery()
        {
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.ReflectionProbeStatic;
            int count = 0;
            foreach (var rootName in StaticRoots)
            {
                var root = GameObject.Find(rootName);
                if (root == null)
                    throw new MissingReferenceException($"'{rootName}' not found in {ScenePath}");

                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Anything the network or physics can move takes its light from the probes instead.
                    if (renderer.GetComponentInParent<NetworkObject>(true) != null ||
                        renderer.GetComponentInParent<Rigidbody>(true) != null)
                        continue;

                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                    var serialized = new SerializedObject(renderer);
                    serialized.FindProperty("m_ScaleInLightmap").floatValue = ScaleFor(renderer.transform, root.transform);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }
            return count;
        }

        private static float ScaleFor(Transform target, Transform root)
        {
            for (var t = target; t != null; t = t == root ? null : t.parent)
                if (CloseUpScale.TryGetValue(t.name, out float scale))
                    return scale;
            return 1f;
        }

        private static void SetLightModes()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // The main light keeps lighting moving things directly; every other light exists only in the bake.
                light.lightmapBakeType = light.type == LightType.Directional ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
                EditorUtility.SetDirty(light);
            }
        }

        // Probes where moving things go: around the table and the projector, through the beam up to the screen,
        // and over the stage in front of it.
        private static void CreateLightProbes()
        {
            const string name = "Light Probes";
            var previous = GameObject.Find(name);
            if (previous != null)
                Object.DestroyImmediate(previous);

            var positions = new List<Vector3>();
            for (float x = -3.5f; x <= 5.51f; x += 1.5f)
                for (float y = 0.25f; y <= 3.26f; y += 1f)
                    for (float z = -5f; z <= 6.01f; z += 1.5f)
                        positions.Add(new Vector3(x, y, z));

            var group = new GameObject(name).AddComponent<LightProbeGroup>();
            group.probePositions = positions.ToArray();
        }
    }
}
