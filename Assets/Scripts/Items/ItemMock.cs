using UnityEngine;

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
        [Tooltip("Name shown to players / used by the shadow-to-platform conversion later.")]
        public string displayName = "Item";

        public ItemCategory category = ItemCategory.Platform;

        [TextArea]
        public string gameplayNote = "";

        [Tooltip("Respawn if the item falls below this height (world Y).")]
        public float killHeight = -2f;

        [Tooltip("Respawn if the item ends up this far from where it started.")]
        public float maxDistanceFromHome = 15f;

        Rigidbody m_Body;
        Vector3 m_HomePosition;
        Quaternion m_HomeRotation;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_HomePosition = transform.position;
            m_HomeRotation = transform.rotation;
        }

        void Update()
        {
            if (transform.position.y < killHeight || Vector3.Distance(transform.position, m_HomePosition) > maxDistanceFromHome)
                ResetToHome();
        }

        /// <summary>Put the item back where it was placed in the scene.</summary>
        public void ResetToHome()
        {
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(m_HomePosition, m_HomeRotation);
            m_Body.position = m_HomePosition;
            m_Body.rotation = m_HomeRotation;
        }
    }
}
