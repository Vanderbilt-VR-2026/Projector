using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Gameplay
{
    // A table object any player can pick up during the build phase. It stays exactly where it is released
    // (kinematic, no gravity) so players can hang objects in the projector beam. Grabbing it takes ownership,
    // and its owner-authority NetworkTransform shares the new position with everyone.
    // Holding it still for a moment confirms the placement: the prop locks there for everyone until the next
    // build. Props may not overlap, so a placement inside another prop cannot be confirmed.
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class NetworkProp : NetworkBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly List<NetworkProp> Props = new List<NetworkProp>();

        [SerializeField] float holdSeconds = 2f;
        [SerializeField] float positionLeeway = 0.04f;
        [SerializeField] float rotationLeeway = 12f;
        [SerializeField] float overlapTolerance = 0.005f;
        [SerializeField] Color confirmingColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] Color overlappingColor = new Color(0.9f, 0.15f, 0.12f);

        public readonly NetworkVariable<bool> Locked = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> HasBeenInteractedWith = new NetworkVariable<bool>();

        XRGrabInteractable grab;
        Rigidbody body;
        Collider[] colliders;
        Renderer[] renderers;
        MaterialPropertyBlock tintBlock;
        bool grabAllowed = true;
        Vector3 holdPosition;
        Quaternion holdRotation;
        Vector3 lastValidPosition;
        Quaternion lastValidRotation;
        float heldStillSeconds;
        bool carried;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            tintBlock = new MaterialPropertyBlock();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;
            Freeze();
        }

        public override void OnNetworkSpawn()
        {
            Props.Add(this);
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            Locked.OnValueChanged += OnLockedChanged;
            ApplyGrabbable();
        }

        public override void OnNetworkDespawn()
        {
            Props.Remove(this);
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            Locked.OnValueChanged -= OnLockedChanged;
        }

        public static void UnlockAll()
        {
            foreach (var prop in Props)
                if (prop != null && prop.IsServer)
                    prop.Locked.Value = false;
        }

        public void SetGrabAllowed(bool allowed)
        {
            grabAllowed = allowed;
            ApplyGrabbable();
            if (!allowed)
                Freeze();
        }

        void ApplyGrabbable() => grab.enabled = grabAllowed && !Locked.Value;

        void OnGrabbed(SelectEnterEventArgs args)
        {
            RequestGrabRpc();
            carried = false;
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
            RestartHold();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (OverlapsAnotherProp())
                transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            RestartHold();
            Freeze();
        }

        void OnLockedChanged(bool previous, bool current)
        {
            if (current && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ApplyGrabbable();
        }

        void Update()
        {
            if (!IsSpawned || Locked.Value || !grab.isSelected)
                return;

            var moved = Vector3.Distance(transform.position, holdPosition) > positionLeeway
                || Quaternion.Angle(transform.rotation, holdRotation) > rotationLeeway;
            if (moved)
            {
                carried = true;
                RestartHold();
            }

            if (OverlapsAnotherProp())
            {
                heldStillSeconds = 0f;
                SetTint(overlappingColor, 1f);
                return;
            }

            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
            if (moved || !carried)
            {
                SetTint(confirmingColor, 0f);
                return;
            }

            heldStillSeconds += Time.deltaTime;
            SetTint(confirmingColor, heldStillSeconds / holdSeconds);
            if (heldStillSeconds >= holdSeconds)
            {
                RestartHold();
                RequestLockRpc();
            }
        }

        void RestartHold()
        {
            holdPosition = transform.position;
            holdRotation = transform.rotation;
            heldStillSeconds = 0f;
            SetTint(confirmingColor, 0f);
        }

        public bool OverlapsAnotherProp()
        {
            foreach (var other in Props)
            {
                if (other == null || other == this || !other.isActiveAndEnabled)
                    continue;

                foreach (var mine in colliders)
                {
                    if (mine == null || !mine.enabled || !mine.gameObject.activeInHierarchy)
                        continue;
                    foreach (var theirs in other.colliders)
                    {
                        if (theirs == null || !theirs.enabled || !theirs.gameObject.activeInHierarchy)
                            continue;
                        if (Physics.ComputePenetration(
                                mine, mine.transform.position, mine.transform.rotation,
                                theirs, theirs.transform.position, theirs.transform.rotation,
                                out _, out var depth)
                            && depth > overlapTolerance)
                            return true;
                    }
                }
            }

            return false;
        }

        void SetTint(Color color, float amount)
        {
            foreach (var propRenderer in renderers)
            {
                var materials = propRenderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (amount <= 0f || materials[i] == null || !materials[i].HasProperty(BaseColorId))
                    {
                        propRenderer.SetPropertyBlock(null, i);
                        continue;
                    }

                    tintBlock.SetColor(BaseColorId, Color.Lerp(materials[i].GetColor(BaseColorId), color, Mathf.Clamp01(amount) * 0.8f));
                    propRenderer.SetPropertyBlock(tintBlock, i);
                }
            }
        }

        void Freeze()
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        [Rpc(SendTo.Server)]
        void RequestGrabRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (NetworkObject.OwnerClientId != sender)
                NetworkObject.ChangeOwnership(sender);
            HasBeenInteractedWith.Value = true;
        }

        [Rpc(SendTo.Server)]
        void RequestLockRpc()
        {
            if (!OverlapsAnotherProp())
                Locked.Value = true;
        }
    }
}
