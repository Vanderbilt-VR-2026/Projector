using Projector.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // Adds the build phase to the projector room without rebuilding the room itself (that needs the local
    // food pack): a projector beam that casts the shadows, a frame marking the part of the wall that becomes
    // the 2D level, networked props on the table, the build timer, and per-player avatars.
    // Re-running replaces the previous "Gameplay" objects.
    public static class ProjectorRoomGameplayBuilder
    {
        private const string ScenePath = "Assets/Scenes/ProjectorRoom.unity";
        private const string GeneratedFolder = "Assets/Generated/Graybox";
        private const string RootName = "Gameplay";
        private const float TableTop = 0.865f;

        [MenuItem("Projector/Add Gameplay To Projector Room")]
        public static void AddGameplay()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var previous = GameObject.Find(RootName);
            if (previous != null)
                Object.DestroyImmediate(previous);

            var root = new GameObject(RootName);
            ShadowCaster caster = CreateProjectorBeam(root.transform);
            CreateScreenFrame(root.transform, caster);
            CreateProps(root.transform);
            NetworkFruit();
            CreateBuildPhase(root.transform, caster);

            var spawner = CreatePlayerSpawner(NetworkPrefabsBuilder.AvatarPath, TableSpots());
            spawner.transform.SetParent(root.transform);
            GameObject.Find("Offline Host").transform.SetParent(root.transform);

            SaveNetworkScene(scene, ScenePath);
            Debug.Log("Gameplay added to " + ScenePath);
        }

        // Players stand along the front of the table, facing the room.
        private static Vector3[] TableSpots()
        {
            var spots = new Vector3[PlayerColors.All.Length];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = new Vector3((i - (spots.Length - 1) / 2f) * 0.8f, 0f, -0.4f);
            return spots;
        }

        private static ShadowCaster CreateProjectorBeam(Transform parent)
        {
            var lens = GameObject.Find("Glass Lens");
            if (lens == null)
                throw new MissingReferenceException("Projector 'Glass Lens' not found in " + ScenePath);

            var beam = new GameObject("Projector Beam");
            beam.transform.SetParent(parent);
            beam.transform.SetPositionAndRotation(lens.transform.position + Vector3.left * 0.03f, Quaternion.LookRotation(Vector3.left));

            var light = beam.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 80f;
            light.innerSpotAngle = 60f;
            light.range = 14f;
            light.intensity = 25f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            return beam.AddComponent<ShadowCaster>();
        }

        // Thin frame on the left wall around the area that maps onto the 2D level.
        private static void CreateScreenFrame(Transform parent, ShadowCaster caster)
        {
            Material frame = CreateMaterial(GeneratedFolder, "DarkGray", new Color(0.12f, 0.13f, 0.14f), 0.3f, 0f);
            var root = new GameObject("Shadow Screen Frame");
            root.transform.SetParent(parent);
            Rect screen = caster.Screen;
            float x = caster.WallX + 0.01f;
            const float bar = 0.03f;
            CreateCube("Top", root.transform, new Vector3(x, screen.yMax, screen.center.x), new Vector3(0.02f, bar, screen.width + bar), frame, false);
            CreateCube("Bottom", root.transform, new Vector3(x, screen.yMin, screen.center.x), new Vector3(0.02f, bar, screen.width + bar), frame, false);
            CreateCube("Left", root.transform, new Vector3(x, screen.center.y, screen.xMin), new Vector3(0.02f, screen.height, bar), frame, false);
            CreateCube("Right", root.transform, new Vector3(x, screen.center.y, screen.xMax), new Vector3(0.02f, screen.height, bar), frame, false);
        }

        // Simple shapes that make useful shadows: long ones become runways, tall ones become walls.
        private static void CreateProps(Transform parent)
        {
            var root = new GameObject("Props");
            root.transform.SetParent(parent);

            (string name, PrimitiveType type, Vector3 scale, Vector3 euler, Color color)[] props =
            {
                ("Block", PrimitiveType.Cube, new Vector3(0.14f, 0.14f, 0.14f), Vector3.zero, new Color(0.85f, 0.45f, 0.35f)),
                ("Plank", PrimitiveType.Cube, new Vector3(0.1f, 0.03f, 0.5f), Vector3.zero, new Color(0.75f, 0.6f, 0.4f)),
                ("Pillar", PrimitiveType.Cylinder, new Vector3(0.07f, 0.15f, 0.07f), Vector3.zero, new Color(0.45f, 0.6f, 0.85f)),
                ("Ball", PrimitiveType.Sphere, new Vector3(0.13f, 0.13f, 0.13f), Vector3.zero, new Color(0.5f, 0.8f, 0.5f)),
                ("Slab", PrimitiveType.Cube, new Vector3(0.2f, 0.05f, 0.3f), Vector3.zero, new Color(0.7f, 0.7f, 0.75f)),
                ("Rod", PrimitiveType.Cylinder, new Vector3(0.04f, 0.3f, 0.04f), new Vector3(90f, 0f, 0f), new Color(0.9f, 0.75f, 0.3f))
            };

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                Material material = CreateMaterial(GeneratedFolder, "Prop" + prop.name, prop.color, 0.3f, 0f);
                float x = (i - (props.Length - 1) / 2f) * 0.45f;
                var go = CreatePrimitive(prop.type, prop.name, root.transform, new Vector3(x, TableTop + 0.2f, 0.15f), prop.scale, prop.euler, material, true);
                // Rest it on the table.
                var bounds = go.GetComponent<Renderer>().bounds;
                go.transform.position += Vector3.up * (TableTop - bounds.min.y);
                MakeNetworkProp(go);
            }
        }

        // The room's fruit (present when the food pack is installed) joins the build as networked props too.
        private static void NetworkFruit()
        {
            var fruit = GameObject.Find("Five Fruit Objects");
            if (fruit == null)
                return;

            foreach (var grab in fruit.GetComponentsInChildren<XRGrabInteractable>())
                if (grab.GetComponent<NetworkProp>() == null)
                    MakeNetworkProp(grab.gameObject);
        }

        private static void MakeNetworkProp(GameObject go)
        {
            // Explicit checks: Unity's fake-null objects break ?? on components.
            var body = go.GetComponent<Rigidbody>();
            if (body == null)
                body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = go.GetComponent<XRGrabInteractable>();
            if (grab == null)
                grab = go.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            // XR grabbing re-parents the object while held; positions sync in world space, so skip parent sync
            // (which would reject the move as invalid parenting).
            var networkObject = go.AddComponent<NetworkObject>();
            networkObject.AutoObjectParentSync = false;
            AddOwnerNetworkTransform(go);
            go.AddComponent<NetworkProp>();
        }

        // Timer and PROJECT NOW button above the shadow screen, facing into the room.
        private static void CreateBuildPhase(Transform parent, ShadowCaster caster)
        {
            var root = new GameObject("Build Phase");
            root.transform.SetParent(parent);
            root.AddComponent<NetworkObject>();
            BuildPhase phase = root.AddComponent<BuildPhase>();

            Rect screen = caster.Screen;
            Canvas canvas = CreateWorldCanvas("Build Timer", root.transform, new Vector2(1800f, 300f), null);
            canvas.transform.localScale = Vector3.one * 0.0025f;
            canvas.transform.SetPositionAndRotation(new Vector3(caster.WallX + 0.04f, screen.yMax + 0.45f, screen.center.x), Quaternion.LookRotation(Vector3.left));

            var panel = CreatePanel("Panel", canvas.transform);
            Text timer = CreateText("", panel, 64, Color.white, "Timer");
            SetAnchors(timer.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.7f, 1f));
            Button project = CreateButton("PROJECT NOW", panel, 56);
            SetAnchors(project.GetComponent<RectTransform>(), new Vector2(0.72f, 0.18f), new Vector2(0.98f, 0.82f));
            UnityEventTools.AddPersistentListener(project.onClick, phase.ProjectNow);

            SetReference(phase, "shadowCaster", caster);
            SetReference(phase, "timerText", timer);
            var projectorButton = GameObject.Find("Control Button 1");
            if (projectorButton != null)
                SetReference(phase, "projectorButton", projectorButton.GetComponent<XRSimpleInteractable>());
            EnsureEventSystem();
        }
    }
}
