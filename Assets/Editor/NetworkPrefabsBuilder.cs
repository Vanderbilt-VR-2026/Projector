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
        private const string ProjectionFolder = "Assets/Generated/Projection";

        [MenuItem("Projector/Build Network Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder(Folder);
            // Tinted per player at runtime through PlayerIdentity, so one white material serves everyone.
            Material body = CreateMaterial(GeneratedFolder, "PlayerBody", Color.white, 0.35f, 0f);

            SaveNetworkPrefab(BuildAvatar(body), AvatarPath);
            SaveNetworkPrefab(BuildRunner(), RunnerPath);
        }

        // A cartoon theatergoer: head in the player's color wearing old red/cyan 3D glasses, a body with a bow tie
        // hung below it, and white gloves. Head and gloves follow the owner's tracked poses; the body follows the
        // head (see NetworkAvatar). A name label floats above for everyone else.
        private static GameObject BuildAvatar(Material body)
        {
            Material glove = CreateMaterial(GeneratedFolder, "PlayerGlove", new Color(0.95f, 0.94f, 0.9f), 0.25f, 0f);
            Material dark = CreateMaterial(GeneratedFolder, "PlayerDark", new Color(0.05f, 0.05f, 0.06f), 0.5f, 0f);
            Material frame = CreateMaterial(GeneratedFolder, "PlayerGlassesFrame", new Color(0.93f, 0.92f, 0.88f), 0.4f, 0f);
            Material red = CreateMaterial(GeneratedFolder, "PlayerGlassesRed", new Color(0.85f, 0.08f, 0.1f), 0.9f, 0f);
            Material cyan = CreateMaterial(GeneratedFolder, "PlayerGlassesCyan", new Color(0.1f, 0.75f, 0.85f), 0.9f, 0f);

            var root = new GameObject("Player Avatar");
            root.AddComponent<NetworkObject>();
            var identity = root.AddComponent<PlayerIdentity>();
            var avatar = root.AddComponent<NetworkAvatar>();

            // The head's origin is between the eyes, where the headset is.
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            AddOwnerNetworkTransform(head);
            var skull = CreatePrimitive(PrimitiveType.Sphere, "Skull", head.transform, new Vector3(0f, 0.01f, -0.07f), new Vector3(0.2f, 0.24f, 0.22f), Vector3.zero, body, false);
            CreateCube("Glasses Frame", head.transform, new Vector3(0f, 0f, 0.035f), new Vector3(0.19f, 0.065f, 0.012f), frame, false);
            CreateCube("Left Lens", head.transform, new Vector3(-0.045f, 0f, 0.042f), new Vector3(0.07f, 0.042f, 0.006f), red, false);
            CreateCube("Right Lens", head.transform, new Vector3(0.045f, 0f, 0.042f), new Vector3(0.07f, 0.042f, 0.006f), cyan, false);
            CreateCube("Left Temple", head.transform, new Vector3(-0.098f, 0.01f, -0.03f), new Vector3(0.008f, 0.012f, 0.13f), frame, false);
            CreateCube("Right Temple", head.transform, new Vector3(0.098f, 0.01f, -0.03f), new Vector3(0.008f, 0.012f, 0.13f), frame, false);

            // The body's origin is the base of the neck, kept upright below the head.
            var torso = new GameObject("Body");
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1.4f, -0.07f);
            var neck = CreatePrimitive(PrimitiveType.Cylinder, "Neck", torso.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.085f, 0.06f, 0.085f), Vector3.zero, body, false);
            var shoulders = CreatePrimitive(PrimitiveType.Capsule, "Shoulders", torso.transform, new Vector3(0f, -0.1f, 0f), new Vector3(0.14f, 0.2f, 0.14f), new Vector3(0f, 0f, 90f), body, false);
            var chest = CreatePrimitive(PrimitiveType.Capsule, "Chest", torso.transform, new Vector3(0f, -0.25f, 0f), new Vector3(0.32f, 0.21f, 0.19f), Vector3.zero, body, false);
            CreatePrimitive(PrimitiveType.Sphere, "Bow Tie Knot", torso.transform, new Vector3(0f, -0.04f, 0.085f), new Vector3(0.028f, 0.028f, 0.02f), Vector3.zero, dark, false);
            CreatePrimitive(PrimitiveType.Cube, "Bow Tie Left", torso.transform, new Vector3(-0.032f, -0.04f, 0.08f), new Vector3(0.04f, 0.04f, 0.012f), new Vector3(0f, 0f, 45f), dark, false);
            CreatePrimitive(PrimitiveType.Cube, "Bow Tie Right", torso.transform, new Vector3(0.032f, -0.04f, 0.08f), new Vector3(0.04f, 0.04f, 0.012f), new Vector3(0f, 0f, 45f), dark, false);

            var left = CreateGlove("Left Hand", root.transform, new Vector3(-0.25f, 1.1f, 0.25f), 1f, glove);
            var right = CreateGlove("Right Hand", root.transform, new Vector3(0.25f, 1.1f, 0.25f), -1f, glove);

            var label = CreateNameLabel(root.transform, new Vector3(0f, 1.88f, 0f), 0.0015f);

            SetReference(avatar, "head", head.transform);
            SetReference(avatar, "body", torso.transform);
            SetReference(avatar, "leftHand", left.transform);
            SetReference(avatar, "rightHand", right.transform);
            SetReference(avatar, "nameLabel", label.canvas.transform);
            SetReferences(identity, "tinted", new Object[] { skull.GetComponent<Renderer>(), neck.GetComponent<Renderer>(), shoulders.GetComponent<Renderer>(), chest.GetComponent<Renderer>() });
            SetReference(identity, "nameLabel", label);
            return root;
        }

        // A cartoon glove around the palm, palm down with fingers forward (+z): a mitten, a thumb on the inside
        // (`thumbSide` is +1 for the left hand, -1 for the right) and a cuff at the wrist.
        private static GameObject CreateGlove(string name, Transform parent, Vector3 localPosition, float thumbSide, Material material)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = localPosition;
            AddOwnerNetworkTransform(hand);
            CreatePrimitive(PrimitiveType.Sphere, "Mitten", hand.transform, new Vector3(0f, 0f, 0.01f), new Vector3(0.095f, 0.05f, 0.12f), Vector3.zero, material, false);
            CreatePrimitive(PrimitiveType.Sphere, "Thumb", hand.transform, new Vector3(thumbSide * 0.047f, -0.004f, 0.012f), new Vector3(0.032f, 0.032f, 0.06f), new Vector3(0f, thumbSide * 30f, 0f), material, false);
            CreatePrimitive(PrimitiveType.Cylinder, "Cuff", hand.transform, new Vector3(0f, 0f, -0.055f), new Vector3(0.085f, 0.012f, 0.07f), new Vector3(90f, 0f, 0f), material, false);
            return hand;
        }

        // A capsule locked to the projection stage's z = 0 plane, simulated by its owner. It's drawn flat
        // (unlit, filmed head-on by the orthographic projector camera) so it reads as a 2D character:
        // player-colored body, dark outline behind it, light visor in front.
        private static GameObject BuildRunner()
        {
            Material fill = CreateUnlitMaterial(ProjectionFolder, "RunnerFill", Color.white);
            Material outline = CreateUnlitMaterial(ProjectionFolder, "RunnerOutline", new Color(0.06f, 0.06f, 0.08f));
            Material visor = CreateUnlitMaterial(ProjectionFolder, "RunnerVisor", new Color(0.92f, 0.96f, 1f));

            var root = CreatePrimitive(PrimitiveType.Capsule, "Runner", null, Vector3.zero, Vector3.one * 0.6f, Vector3.zero, fill, true);
            // Runner-runner collisions are off on this layer (see RunnerController).
            root.layer = LayerMask.NameToLayer("Runner");
            root.GetComponent<CapsuleCollider>().sharedMaterial = CreateFrictionlessMaterial();
            root.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

            root.AddComponent<NetworkObject>();
            AddOwnerNetworkTransform(root, syncRotation: false);
            var identity = root.AddComponent<PlayerIdentity>();
            root.AddComponent<RunnerController>();

            // Larger copy just behind (farther from the camera) reads as an outline.
            CreatePrimitive(PrimitiveType.Capsule, "Outline", root.transform, new Vector3(0f, 0f, 0.3f), Vector3.one * 1.22f, Vector3.zero, outline, false);
            CreateCube("Visor", root.transform, new Vector3(0.12f, 0.42f, -0.6f), new Vector3(0.62f, 0.26f, 0.1f), visor, false);
            var label = CreateNameLabel(root.transform, new Vector3(0f, 1.6f, 0f), 0.006f / 0.6f);

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
