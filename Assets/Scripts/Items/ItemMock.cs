using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProjectorGame.Items
{
    public enum ItemCategory
    {
        Platform,
        Movement,
        Hazard,
        Bonus,
    }

    /// <summary>
    /// Graybox stand-in for a placeable game item (Ultimate Chicken Horse style).
    /// Players pick these up off the table; their shadows become the 2D level.
    /// Respawns on its table spot if it gets dropped out of the room.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ItemMock : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly HashSet<ItemMock> ActiveItems = new HashSet<ItemMock>();

        [Tooltip("Name shown to players / used by the shadow-to-platform conversion later.")]
        public string displayName = "Item";

        public ItemCategory category = ItemCategory.Platform;

        [TextArea]
        public string gameplayNote = "";

        [Tooltip("Respawn if the item falls below this height (world Y).")]
        public float killHeight = -2f;

        [Tooltip("Respawn if the item ends up this far from where it started.")]
        public float maxDistanceFromHome = 15f;

        [Header("Placement confirmation")]
        [SerializeField, Min(0.1f)] float holdSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("Maximum position drift that still counts as holding steady.")]
        float positionLeeway = 0.04f;
        [SerializeField, Min(0f), Tooltip("Maximum rotation drift that still counts as holding steady.")]
        float rotationLeeway = 12f;
        [SerializeField, Min(0f), Tooltip("Tiny collider penetrations below this depth are treated as contact, not overlap.")]
        float overlapTolerance = 0.005f;
        [SerializeField] Color confirmingColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] Color overlappingColor = new Color(0.9f, 0.15f, 0.12f);

        Rigidbody m_Body;
        XRGrabInteractable m_Grab;
        Collider[] m_Colliders;
        Renderer[] m_Renderers;
        MaterialPropertyBlock m_TintBlock;
        Vector3 m_HomePosition;
        Quaternion m_HomeRotation;
        Vector3 m_HoldPosition;
        Quaternion m_HoldRotation;
        Vector3 m_LastValidPosition;
        Quaternion m_LastValidRotation;
        float m_HeldStillSeconds;
        bool m_Carried;
        bool m_GrabAllowed = true;

        public bool HasBeenInteractedWith { get; private set; }
        public bool IsLocked { get; private set; }

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Colliders = GetComponentsInChildren<Collider>();
            m_Renderers = GetComponentsInChildren<Renderer>();
            m_TintBlock = new MaterialPropertyBlock();
            if (m_Grab != null)
            {
                // Velocity tracking keeps held items solid so they cannot be dragged through each other.
                m_Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                m_Grab.throwOnDetach = false;
                m_Grab.selectEntered.AddListener(OnGrabbed);
                m_Grab.selectExited.AddListener(OnReleased);
            }
            m_HomePosition = transform.position;
            m_HomeRotation = transform.rotation;
            m_LastValidPosition = m_HomePosition;
            m_LastValidRotation = m_HomeRotation;
            SetStatic();
        }

        void OnEnable() => ActiveItems.Add(this);

        void OnDisable() => ActiveItems.Remove(this);

        void OnDestroy()
        {
            if (m_Grab != null)
            {
                m_Grab.selectEntered.RemoveListener(OnGrabbed);
                m_Grab.selectExited.RemoveListener(OnReleased);
            }
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            HasBeenInteractedWith = true;
            m_Carried = false;
            m_LastValidPosition = transform.position;
            m_LastValidRotation = transform.rotation;
            RestartHold();
            m_Body.isKinematic = false;
            m_Body.useGravity = false;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            // A forced or very fast move can briefly penetrate another item. Never let an invalid pose settle.
            if (OverlapsAnotherItem())
                transform.SetPositionAndRotation(m_LastValidPosition, m_LastValidRotation);
            RestartHold();
            SetStatic();
        }

        public void SetGrabAllowed(bool allowed)
        {
            m_GrabAllowed = allowed;
            if (!allowed && m_Grab != null && m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            ApplyGrabState();
            if (!allowed)
                SetStatic();
        }

        void ApplyGrabState()
        {
            if (m_Grab != null)
                m_Grab.enabled = m_GrabAllowed && !IsLocked;
        }

        void SetStatic()
        {
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Body.isKinematic = true;
            m_Body.useGravity = false;
        }

        void Update()
        {
            if (!IsLocked && (transform.position.y < killHeight || Vector3.Distance(transform.position, m_HomePosition) > maxDistanceFromHome))
                ResetToHome();

            if (!IsLocked && m_Grab != null && m_Grab.isSelected)
                UpdatePlacementConfirmation();
        }

        void UpdatePlacementConfirmation()
        {
            var moved = Vector3.Distance(transform.position, m_HoldPosition) > positionLeeway
                || Quaternion.Angle(transform.rotation, m_HoldRotation) > rotationLeeway;
            if (moved)
            {
                m_Carried = true;
                RestartHold();
            }

            if (OverlapsAnotherItem())
            {
                m_HeldStillSeconds = 0f;
                SetTint(overlappingColor, 1f);
                return;
            }

            m_LastValidPosition = transform.position;
            m_LastValidRotation = transform.rotation;
            if (moved || !m_Carried)
            {
                SetTint(confirmingColor, 0f);
                return;
            }

            m_HeldStillSeconds += Time.deltaTime;
            SetTint(confirmingColor, m_HeldStillSeconds / holdSeconds);
            if (m_HeldStillSeconds >= holdSeconds)
                ConfirmPlacement();
        }

        void RestartHold()
        {
            m_HoldPosition = transform.position;
            m_HoldRotation = transform.rotation;
            m_HeldStillSeconds = 0f;
            SetTint(confirmingColor, 0f);
        }

        void ConfirmPlacement()
        {
            // Re-check at the decision point so an invalid placement can never become locked.
            if (OverlapsAnotherItem())
            {
                RestartHold();
                return;
            }

            IsLocked = true;
            if (m_Grab != null && m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            ApplyGrabState();
            SetStatic();
            SetTint(confirmingColor, 0f);
        }

        public bool OverlapsAnotherItem()
        {
            foreach (var other in ActiveItems)
            {
                if (other == null || other == this || !other.isActiveAndEnabled)
                    continue;

                foreach (var mine in m_Colliders)
                {
                    if (mine == null || !mine.enabled)
                        continue;
                    foreach (var theirs in other.m_Colliders)
                    {
                        if (theirs == null || !theirs.enabled)
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
            foreach (var itemRenderer in m_Renderers)
            {
                var materials = itemRenderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (amount <= 0f || materials[i] == null || !materials[i].HasProperty(BaseColorId))
                    {
                        itemRenderer.SetPropertyBlock(null, i);
                        continue;
                    }

                    m_TintBlock.SetColor(
                        BaseColorId,
                        Color.Lerp(materials[i].GetColor(BaseColorId), color, Mathf.Clamp01(amount) * 0.8f));
                    itemRenderer.SetPropertyBlock(m_TintBlock, i);
                }
            }
        }

        /// <summary>Put the item back where it was placed in the scene.</summary>
        public void ResetToHome()
        {
            IsLocked = false;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(m_HomePosition, m_HomeRotation);
            m_Body.position = m_HomePosition;
            m_Body.rotation = m_HomeRotation;
            m_LastValidPosition = m_HomePosition;
            m_LastValidRotation = m_HomeRotation;
            RestartHold();
            ApplyGrabState();
            SetStatic();
        }
    }
}
