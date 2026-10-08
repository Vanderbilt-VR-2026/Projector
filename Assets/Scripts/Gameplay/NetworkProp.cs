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
        // Hands are never perfectly still in VR: drifting less than this from where the hold began still counts.
        [SerializeField] float positionLeeway = 0.04f;
        [SerializeField] float rotationLeeway = 12f;
        // Props pressed against each other sink in slightly; only deeper contact than this counts as overlapping.
        [SerializeField] float overlapTolerance = 0.005f;
        [SerializeField] Color confirmingColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] Color overlappingColor = new Color(0.9f, 0.15f, 0.12f);

        public readonly NetworkVariable<bool> Locked = new NetworkVariable<bool>();

        XRGrabInteractable grab;
        Rigidbody body;
        Collider[] colliders;
        Renderer[] renderers;
        MaterialPropertyBlock tintBlock;
        bool grabAllowed = true;
        Vector3 holdPosition;
        Quaternion holdRotation;
        float heldStillSeconds;
        // False until the prop has been carried away from where it was picked up, so lifting it never confirms it.
        bool carried;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            tintBlock = new MaterialPropertyBlock();
            // Velocity tracking keeps a held prop solid, so it is stopped by other props instead of passing through.
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
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

        // Host: a new build starts with every prop movable again.
        public static void UnlockAll()
        {
            foreach (var prop in Props)
                prop.Locked.Value = false;
        }

        // Props cannot be grabbed during the race, and a locked prop cannot be grabbed at all.
        public void SetGrabAllowed(bool allowed)
        {
            grabAllowed = allowed;
            ApplyGrabbable();
        }

        void ApplyGrabbable() => grab.enabled = grabAllowed && !Locked.Value;

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!IsOwner)
                RequestOwnershipRpc();
            carried = false;
            RestartHold();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            RestartHold();
            Freeze();
        }

        void OnLockedChanged(bool previous, bool current)
        {
            if (current && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ApplyGrabbable();
        }

        // Only runs for the player holding the prop: times how long it has been held still.
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

            if (moved || !carried)
            {
                SetTint(confirmingColor, 0f);
                return;
            }

            heldStillSeconds += Time.deltaTime;
            SetTint(confirmingColor, heldStillSeconds / holdSeconds);
            if (heldStillSeconds >= holdSeconds)
            {
                // If the host refuses, the hold simply starts over.
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
                if (other == this)
                    continue;

                foreach (var mine in colliders)
                {
                    foreach (var theirs in other.colliders)
                    {
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

        // Blends every material toward `color`; an amount of 0 restores the prop's own colors.
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
        void RequestOwnershipRpc(RpcParams rpcParams = default)
        {
            NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
        }

        // The host has the last word, so two players cannot lock props into each other at the same moment.
        [Rpc(SendTo.Server)]
        void RequestLockRpc()
        {
            if (!OverlapsAnotherProp())
                Locked.Value = true;
        }
    }
}
