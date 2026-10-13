using Projector.Networking;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Projector.Gameplay
{
    // A player's head and hands in the 3D scenes. The owner copies its XR rig's poses onto the head/hand
    // children every frame (their owner-authority NetworkTransforms sync them); everyone else sees the result.
    // The owner hides its own avatar so it doesn't render inside the headset.
    public class NetworkAvatar : NetworkBehaviour
    {
        static readonly string[] LeftNames = { "Left Hand", "Left Controller" };
        static readonly string[] RightNames = { "Right Hand", "Right Controller" };

        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform nameLabel;

        Transform rigHead;
        Transform[] rigLefts;
        Transform[] rigRights;

        public override void OnNetworkSpawn()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (!IsOwner)
                return;

            foreach (var avatarRenderer in GetComponentsInChildren<Renderer>())
                avatarRenderer.enabled = false;
            nameLabel.gameObject.SetActive(false);

            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null)
            {
                rigHead = origin.Camera.transform;
                rigLefts = FindAll(origin.transform, LeftNames);
                rigRights = FindAll(origin.transform, RightNames);
            }

            if (PlayerSpawner.Current != null)
                PlayerSpawner.Current.PlaceLocalRig(identity.Slot.Value);
        }

        void LateUpdate()
        {
            if (IsOwner)
            {
                if (rigHead == null)
                    return;

                head.SetPositionAndRotation(rigHead.position, rigHead.rotation);
                CopyPose(rigLefts, leftHand);
                CopyPose(rigRights, rightHand);
                return;
            }

            // Keep the name above the head and turned toward the local viewer.
            var viewer = Camera.main;
            nameLabel.position = head.position + Vector3.up * 0.28f;
            if (viewer != null)
                nameLabel.rotation = Quaternion.LookRotation(nameLabel.position - viewer.transform.position);
        }

        // Hands and controllers swap depending on what's tracked; follow whichever is active.
        static void CopyPose(Transform[] sources, Transform target)
        {
            foreach (var source in sources)
            {
                if (source != null && source.gameObject.activeInHierarchy)
                {
                    target.SetPositionAndRotation(source.position, source.rotation);
                    return;
                }
            }
        }

        static Transform[] FindAll(Transform root, string[] names)
        {
            var found = new Transform[names.Length];
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                var index = System.Array.IndexOf(names, child.name);
                if (index >= 0 && found[index] == null)
                    found[index] = child;
            }
            return found;
        }
    }
}
