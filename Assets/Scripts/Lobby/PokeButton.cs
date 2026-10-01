using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ProjectorGame.Lobby
{
    /// <summary>
    /// Physical button that can be poked with a finger (XR Poke Filter) or pointed at and selected with the ray.
    /// The cap sinks in and lights up, and the controller gets a haptic tap.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class PokeButton : MonoBehaviour
    {
        public UnityEvent onPressed = new UnityEvent();

        [Header("Visuals")]
        public Transform cap;
        public Renderer capRenderer;
        public Color idleColor = new Color(0.2f, 0.6f, 1f);
        public Color hoverColor = new Color(0.5f, 0.85f, 1f);
        public Color pressColor = Color.white;
        public float pressDepth = 0.015f;

        [Tooltip("Ignore presses that come faster than this (seconds).")]
        public float cooldown = 0.4f;

        XRSimpleInteractable m_Interactable;
        MaterialPropertyBlock m_Block;
        Vector3 m_CapRest;
        float m_PressedUntil;
        float m_LastPress = -10f;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            m_Block = new MaterialPropertyBlock();
            if (cap != null)
                m_CapRest = cap.localPosition;
            m_Interactable.selectEntered.AddListener(OnSelected);
            SetColor(idleColor);
        }

        void OnDestroy()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            if (Time.time - m_LastPress < cooldown)
                return;
            m_LastPress = Time.time;
            m_PressedUntil = Time.time + 0.2f;

            var hand = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (hand != null)
                hand.SendHapticImpulse(0.5f, 0.05f);

            onPressed.Invoke();
        }

        void Update()
        {
            bool pressed = Time.time < m_PressedUntil || m_Interactable.isSelected;
            if (cap != null)
            {
                Vector3 target = m_CapRest + (pressed ? Vector3.forward * pressDepth : Vector3.zero);
                cap.localPosition = Vector3.Lerp(cap.localPosition, target, Time.deltaTime * 20f);
            }
            SetColor(pressed ? pressColor : (m_Interactable.isHovered ? hoverColor : idleColor));
        }

        void SetColor(Color c)
        {
            if (capRenderer == null)
                return;
            capRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", c * 0.8f);
            capRenderer.SetPropertyBlock(m_Block);
        }
    }
}
