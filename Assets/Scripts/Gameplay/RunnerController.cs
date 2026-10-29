using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Projector.Gameplay
{
    // A player's racer on the projection stage. The owner simulates it (left thumbstick or A/D to run, A/X button or Space to jump)
    // and its owner-authority NetworkTransform shows it to everyone else. The owner reports coins, falls
    // (including touching a hazard) and crossing the finish to the race manager. Bounce pads launch it.
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class RunnerController : NetworkBehaviour
    {
        [SerializeField] float runSpeed = 6f;
        [SerializeField] float jumpSpeed = 8.5f;

        Rigidbody body;
        CapsuleCollider capsule;
        InputAction move;
        InputAction jump;
        Vector3 spawnPosition;
        bool jumpQueued;
        bool finished;
        float bounceSpeed;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();

            // Runners pass through each other; the race is against the level, not body-blocking. A layer that
            // ignores itself covers every pair, whatever order the runners spawn in.
            gameObject.layer = RunnerLayer;
            Physics.IgnoreLayerCollision(RunnerLayer, RunnerLayer);
        }

        static int RunnerLayer => LayerMask.NameToLayer("Runner");

        public override void OnNetworkSpawn()
        {
            body.isKinematic = !IsOwner;

            if (!IsOwner)
                return;

            // Start (and respawn) exactly on this slot's start point. A client's copy is created before Netcode
            // places it, and the rigidbody would otherwise keep the creation pose.
            var slot = GetComponent<PlayerIdentity>().Slot.Value;
            spawnPosition = RaceManager.Current != null ? RaceManager.Current.StartPoint(slot) : transform.position;
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
            if (race == null || ProjectorGame.Current == null || !race.IsRunning || finished)
            {
                velocity.x = 0f;
                if (ProjectorGame.Current != null && ProjectorGame.Current.IsLocallyPaused)
                    velocity.y = 0f;
                body.linearVelocity = velocity;
                jumpQueued = false;
                return;
            }

            var grounded = IsGrounded();
            velocity.x = move.ReadValue<Vector2>().x * runSpeed;
            if (jumpQueued && grounded)
                velocity.y = jumpSpeed;
            jumpQueued = false;
            if (bounceSpeed > 0f)
            {
                velocity.y = Mathf.Max(velocity.y, bounceSpeed);
                bounceSpeed = 0f;
            }
            body.linearVelocity = velocity;

            var level = race.ToLevel(transform.position);
            if (level.y < race.PitHeight)
            {
                Respawn();
                race.ReportFallRpc();
            }
            else if (grounded && level.x >= race.FinishX)
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

        // Hazards and bounce pads are solid shapes, so they're found through collisions (only the owner's
        // runner is simulated, so only the owner gets these).
        void OnCollisionEnter(Collision collision) => HandleContact(collision, true);

        void OnCollisionStay(Collision collision) => HandleContact(collision, false);

        void HandleContact(Collision collision, bool justLanded)
        {
            var race = RaceManager.Current;
            if (!IsOwner || finished || race == null || !race.IsRunning)
                return;

            if (collision.collider.GetComponentInParent<Hazard>() != null)
            {
                Respawn();
                race.ReportFallRpc();
                return;
            }

            var pad = collision.collider.GetComponentInParent<BouncePad>();
            if (pad != null && justLanded && collision.contactCount > 0
                && collision.GetContact(0).point.y < transform.position.y)
            {
                bounceSpeed = pad.LaunchSpeed;
                pad.Flash();
            }
        }

        // Level geometry only: another runner underfoot isn't ground, so nobody can stand or jump on a head.
        bool IsGrounded()
        {
            var radius = capsule.radius * transform.lossyScale.x * 0.9f;
            var halfHeight = capsule.height * transform.lossyScale.y / 2f;
            return Physics.SphereCast(transform.position, radius, Vector3.down, out _, halfHeight - radius + 0.08f,
                Physics.DefaultRaycastLayers & ~(1 << RunnerLayer), QueryTriggerInteraction.Ignore);
        }

        void Respawn()
        {
            body.linearVelocity = Vector3.zero;
            body.position = spawnPosition;
            transform.position = spawnPosition;
        }
    }
}
