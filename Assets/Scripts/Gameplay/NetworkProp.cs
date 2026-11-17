using System.Collections.Generic;
using Projector.Audio;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Gameplay
{
    // A table object any player can pick up during the build phase. It stays exactly where it is released
    // (kinematic, no gravity) so players can hang objects in the projector beam. Grabbing it takes ownership,
    // and its owner-authority NetworkTransform shares the new position with everyone.
    // Holding it still for a moment confirms the placement: the prop locks there for everyone until the next
    // build ends. Items included in the race are then committed for the rest of the session.
    // Props may not overlap, so a placement inside another prop cannot be confirmed.
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
        public readonly NetworkVariable<bool> Committed = new NetworkVariable<bool>();
        readonly NetworkVariable<Vector3> committedPosition = new NetworkVariable<Vector3>();
        readonly NetworkVariable<Quaternion> committedRotation = new NetworkVariable<Quaternion>(Quaternion.identity);

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
        ulong lastPlayerOwner;

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
            if (IsServer)
            {
                lastPlayerOwner = OwnerClientId;
                NetworkManager.OnClientDisconnectCallback += OnOwnerDisconnected;
            }
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            Locked.OnValueChanged += OnLockedChanged;
            Committed.OnValueChanged += OnCommittedChanged;
            if (ProjectorGame.Current != null)
                grabAllowed = ProjectorGame.Current.Phase.Value == GamePhase.Building && !ProjectorGame.Current.IsLocallyPaused;
            RestoreCommittedPose();
            ApplyGrabbable();
        }

        public override void OnNetworkDespawn()
        {
            Props.Remove(this);
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnOwnerDisconnected;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            Locked.OnValueChanged -= OnLockedChanged;
            Committed.OnValueChanged -= OnCommittedChanged;
        }

        public static void UnlockAll()
        {
            foreach (var prop in Props)
                if (prop != null && prop.IsServer && !prop.Committed.Value)
                    prop.Locked.Value = false;
        }

        void OnOwnerDisconnected(ulong clientId)
        {
            // Netcode transfers surviving objects to the server before invoking this callback.
            if (!IsServer || !IsSpawned || NetworkManager.ShutdownInProgress ||
                Committed.Value || lastPlayerOwner != clientId ||
                !TryGetComponent<global::ProjectorGame.Items.ItemMock>(out var item))
                return;

            NetworkObject.RemoveOwnership();
            lastPlayerOwner = Unity.Netcode.NetworkManager.ServerClientId;
            Locked.Value = false;
            HasBeenInteractedWith.Value = false;
            carried = false;
            item.ResetToHome();
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
            RestartHold();
            // Teleport clears interpolation so everyone sees the item back on the table immediately.
            if (TryGetComponent<NetworkTransform>(out var networkTransform))
                networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        public void SetGrabAllowed(bool allowed)
        {
            grabAllowed = allowed;
            ApplyGrabbable();
            if (!allowed)
                Freeze();
        }

        void ApplyGrabbable() => grab.enabled = grabAllowed && !Locked.Value && !Committed.Value;

        // The host's final pose survives ownership changes, late packets and late joiners.
        public void CommitPlacement()
        {
            if (!IsServer || Committed.Value)
                return;
            committedPosition.Value = transform.position;
            committedRotation.Value = transform.rotation;
            Committed.Value = true;
            Locked.Value = true;
            NetworkObject.RemoveOwnership();
            RestoreCommittedPose();
        }

        void OnCommittedChanged(bool previous, bool current)
        {
            if (current && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ApplyGrabbable();
            RestoreCommittedPose();
        }

        void LateUpdate() => RestoreCommittedPose();

        void RestoreCommittedPose()
        {
            if (!IsSpawned || !Committed.Value)
                return;
            Freeze();
            transform.SetPositionAndRotation(committedPosition.Value, committedRotation.Value);
            body.position = committedPosition.Value;
            body.rotation = committedRotation.Value;
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            RequestGrabRpc();
            GameAudio.Thunk(pickingUp: true);
            carried = false;
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;
            RestartHold();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!Committed.Value && OverlapsAnotherProp())
                transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            GameAudio.Thunk(pickingUp: false);
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
            if (Committed.Value || Locked.Value || ProjectorGame.Current == null ||
                ProjectorGame.Current.Phase.Value != GamePhase.Building)
                return;
            var sender = rpcParams.Receive.SenderClientId;
            if (NetworkObject.OwnerClientId != sender)
                NetworkObject.ChangeOwnership(sender);
            lastPlayerOwner = sender;
            HasBeenInteractedWith.Value = true;
        }

        [Rpc(SendTo.Server)]
        void RequestLockRpc(RpcParams rpcParams = default)
        {
            if (Committed.Value || ProjectorGame.Current == null ||
                ProjectorGame.Current.Phase.Value != GamePhase.Building ||
                rpcParams.Receive.SenderClientId != OwnerClientId)
                return;
            if (!OverlapsAnotherProp())
                Locked.Value = true;
        }
    }
}
