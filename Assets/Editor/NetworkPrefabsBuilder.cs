using Projector.Gameplay;
using Projector.Networking;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Projector.Editor.SceneBuildUtility;

namespace Projector.Editor
{
    // Builds the prefabs spawned per player. They live under Resources/NetworkPrefabs so SessionService can
    // register them with the NetworkManager on every peer.
    public static class NetworkPrefabsBuilder
    {
        public const string Folder = "Assets/Resources/" + SessionService.NetworkPrefabsFolder;
        public const string AvatarPath = Folder + "/Player Avatar.prefab";
        public const string RunnerPath = Folder + "/Runner.prefab";
        private const string GeneratedFolder = "Assets/Generated/Players";

        [MenuItem("Projector/Build Network Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder(Folder);
            // Tinted per player at runtime through PlayerIdentity, so one white material serves everyone.
            Material body = CreateMaterial(GeneratedFolder, "PlayerBody", Color.white, 0.35f, 0f);
            Material visor = CreateMaterial(GeneratedFolder, "PlayerVisor", new Color(0.05f, 0.06f, 0.08f), 0.8f, 0f);

            SaveNetworkPrefab(BuildAvatar(body, visor), AvatarPath);
            SaveNetworkPrefab(BuildRunner(body, visor), RunnerPath);
        }

        // Head and hands, each following the owner's tracked pose, with a name label for everyone else.
        private static GameObject BuildAvatar(Material body, Material visor)
        {
            var root = new GameObject("Player Avatar");
            root.AddComponent<NetworkObject>();
            var identity = root.AddComponent<PlayerIdentity>();
            var avatar = root.AddComponent<NetworkAvatar>();

            var head = CreatePrimitive(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.6f, 0f), Vector3.one * 0.24f, Vector3.zero, body, false);
            AddOwnerNetworkTransform(head);
            CreateCube("Visor", head.transform, new Vector3(0f, 0.08f, 0.4f), new Vector3(0.8f, 0.3f, 0.3f), visor, false);

            var left = CreatePrimitive(PrimitiveType.Sphere, "Left Hand", root.transform, new Vector3(-0.25f, 1.1f, 0.25f), Vector3.one * 0.09f, Vector3.zero, body, false);
            AddOwnerNetworkTransform(left);
            var right = CreatePrimitive(PrimitiveType.Sphere, "Right Hand", root.transform, new Vector3(0.25f, 1.1f, 0.25f), Vector3.one * 0.09f, Vector3.zero, body, false);
            AddOwnerNetworkTransform(right);

            var label = CreateNameLabel(root.transform, new Vector3(0f, 1.88f, 0f), 0.0015f);

            SetReference(avatar, "head", head.transform);
            SetReference(avatar, "leftHand", left.transform);
            SetReference(avatar, "rightHand", right.transform);
            SetReference(avatar, "nameLabel", label.canvas.transform);
            SetReferences(identity, "tinted", new Object[] { head.GetComponent<Renderer>(), left.GetComponent<Renderer>(), right.GetComponent<Renderer>() });
            SetReference(identity, "nameLabel", label);
            return root;
        }

        // A capsule locked to the z = 0 plane, simulated by its owner.
        private static GameObject BuildRunner(Material body, Material visor)
        {
            var root = CreatePrimitive(PrimitiveType.Capsule, "Runner", null, Vector3.zero, Vector3.one * 0.4f, Vector3.zero, body, true);
            var frictionless = CreateFrictionlessMaterial();
            root.GetComponent<CapsuleCollider>().sharedMaterial = frictionless;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

            root.AddComponent<NetworkObject>();
            AddOwnerNetworkTransform(root, syncRotation: false);
            var identity = root.AddComponent<PlayerIdentity>();
            root.AddComponent<RunnerController>();

            // Visor faces the viewer so the capsule reads as a character.
            CreateCube("Visor", root.transform, new Vector3(0f, 0.45f, -0.4f), new Vector3(0.7f, 0.25f, 0.2f), visor, false);
            var label = CreateNameLabel(root.transform, new Vector3(0f, 1.6f, 0f), 0.0015f / 0.4f);

            SetReferences(identity, "tinted", new Object[] { root.GetComponent<Renderer>() });
            SetReference(identity, "nameLabel", label);
            return root;
        }

        private static Text CreateNameLabel(Transform parent, Vector3 localPosition, float scale)
        {
            Canvas canvas = CreateWorldCanvas("Name Label", parent, new Vector2(600f, 100f), null);
            canvas.transform.localPosition = localPosition;
            canvas.transform.localScale = Vector3.one * scale;
            Text text = CreateText("Player", canvas.transform, 64, Color.white, "Name");
            SetFullSize(text.rectTransform);
            return text;
        }

        private static PhysicsMaterial CreateFrictionlessMaterial()
        {
            const string path = GeneratedFolder + "/Frictionless.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("Frictionless");
                AssetDatabase.CreateAsset(material, path);
            }
            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
