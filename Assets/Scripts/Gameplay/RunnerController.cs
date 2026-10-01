using Projector.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Projector.Gameplay
{
    // A player's 2D racer. The owner simulates it (left thumbstick or A/D to run, A/X button or Space to jump)
    // and its owner-authority NetworkTransform shows it to everyone else. The owner reports coins, falls
    // and crossing the finish to the race manager.
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class RunnerController : NetworkBehaviour
    {
        [SerializeField] float runSpeed = 5f;
        [SerializeField] float jumpSpeed = 7f;

        Rigidbody body;
        CapsuleCollider capsule;
        InputAction move;
        InputAction jump;
        Vector3 spawnPosition;
        bool jumpQueued;
        bool finished;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
        }

        public override void OnNetworkSpawn()
        {
            body.isKinematic = !IsOwner;

            // Runners pass through each other; the race is against the level, not body-blocking.
            foreach (var other in FindObjectsByType<RunnerController>(FindObjectsSortMode.None))
                if (other != this)
                    Physics.IgnoreCollision(capsule, other.capsule);

            if (!IsOwner)
                return;

            // Start (and respawn) exactly on this slot's spawn point. A client's copy is created before Netcode
            // places it, and the rigidbody would otherwise keep the creation pose.
            var slot = GetComponent<PlayerIdentity>().Slot.Value;
            spawnPosition = PlayerSpawner.Current != null ? PlayerSpawner.Current.SpawnPoint(slot).position : transform.position;
            Respawn();

            move = new InputAction("Run", InputActionType.Value);
            move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            move.AddCompositeBinding("2DVector").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s");
            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            jump.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            jump.AddBinding("<Keyboard>/space");
            // A callback, not WasPressedThisFrame: never misses a press however input updates line up with frames.
            jump.performed += OnJump;
            move.Enable();
            jump.Enable();

            if (PlayerSpawner.Current != null)
                PlayerSpawner.Current.PlaceLocalRig(slot);
        }

        public override void OnNetworkDespawn()
        {
            move?.Dispose();
            jump?.Dispose();
        }

        void OnJump(InputAction.CallbackContext context) => jumpQueued = true;

        void FixedUpdate()
        {
            if (!IsOwner)
                return;

            var race = RaceManager.Current;
            var velocity = body.linearVelocity;
            velocity.z = 0f;
            if (race == null || !race.IsRunning || finished)
            {
                velocity.x = 0f;
                body.linearVelocity = velocity;
                jumpQueued = false;
                return;
            }

            var grounded = IsGrounded();
            velocity.x = move.ReadValue<Vector2>().x * runSpeed;
            if (jumpQueued && grounded)
                velocity.y = jumpSpeed;
            jumpQueued = false;
            body.linearVelocity = velocity;

            if (transform.position.y < race.PitHeight)
            {
                Respawn();
                race.ReportFallRpc();
            }
            else if (grounded && transform.position.x >= race.FinishX)
            {
                finished = true;
                race.ReportFinishRpc();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsOwner || !other.TryGetComponent(out Coin coin))
                return;

            coin.Collect();
            if (RaceManager.Current != null)
                RaceManager.Current.ReportCoinRpc();
        }

        bool IsGrounded()
        {
            var radius = capsule.radius * transform.lossyScale.x * 0.9f;
            var halfHeight = capsule.height * transform.lossyScale.y / 2f;
            return Physics.SphereCast(transform.position, radius, Vector3.down, out _, halfHeight - radius + 0.08f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void Respawn()
        {
            body.linearVelocity = Vector3.zero;
            body.position = spawnPosition;
            transform.position = spawnPosition;
        }
    }
}
