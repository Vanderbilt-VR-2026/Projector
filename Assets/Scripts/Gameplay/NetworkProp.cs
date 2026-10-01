using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Gameplay
{
    // A table object any player can pick up during the build phase. It stays exactly where it is released
    // (kinematic, no gravity) so players can hang objects in the projector beam. Grabbing it takes ownership,
    // and its owner-authority NetworkTransform shares the new position with everyone.
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class NetworkProp : NetworkBehaviour
    {
        XRGrabInteractable grab;
        Rigidbody body;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            Freeze();
        }

        public override void OnNetworkSpawn()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        public override void OnNetworkDespawn()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!IsOwner)
                RequestOwnershipRpc();
        }

        void OnReleased(SelectExitEventArgs args) => Freeze();

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
    }
}
