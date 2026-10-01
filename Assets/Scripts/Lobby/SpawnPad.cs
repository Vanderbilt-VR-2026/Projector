using UnityEngine;
using UnityEngine.UI;

namespace ProjectorGame.Lobby
{
    /// <summary>
    /// A coloured player spot in the lobby. Glows brighter and shows READY while the local player stands on it.
    /// Networking can later call SetClaimed() to show which remote player owns the pad.
    /// </summary>
    public class SpawnPad : MonoBehaviour
    {
        public int playerIndex;
        public Color color = Color.cyan;
        public Renderer ringRenderer;
        public Text label;
        public float radius = 0.45f;

        public bool LocalPlayerOnPad { get; private set; }
        public bool Claimed { get; private set; }

        MaterialPropertyBlock m_Block;
        Transform m_Head;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
        }

        public void SetClaimed(bool claimed)
        {
            Claimed = claimed;
        }

        void Update()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;

            if (m_Head != null)
            {
                Vector3 d = m_Head.position - transform.position;
                d.y = 0f;
                LocalPlayerOnPad = d.magnitude < radius;
            }

            bool lit = LocalPlayerOnPad || Claimed;
            float pulse = lit ? 2.2f + Mathf.Sin(Time.time * 6f) * 0.6f : 0.6f + Mathf.Sin(Time.time * 1.5f + playerIndex) * 0.15f;

            if (ringRenderer != null)
            {
                ringRenderer.GetPropertyBlock(m_Block);
                m_Block.SetColor("_BaseColor", color);
                m_Block.SetColor("_EmissionColor", color * pulse);
                ringRenderer.SetPropertyBlock(m_Block);
            }

            if (label != null)
                label.text = "P" + (playerIndex + 1) + (lit ? "\nREADY" : "");
        }
    }
}
