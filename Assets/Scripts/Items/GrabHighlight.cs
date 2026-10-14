using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProjectorGame.Items
{
    /// <summary>
    /// Item grab clarity (#58): when a hand or ray hovers a grabbable item, a glowing outline appears around it,
    /// and it gets brighter while the item is held. Locked or disabled items can't be hovered, so they never glow.
    ///
    /// The outline is an "inverted hull": a slightly larger copy of each mesh that only draws its back faces.
    /// It lives in its own root object (not a child of the item) and casts no shadow, so it never changes the
    /// item's projected shadow or its colliders, and it doesn't touch the item's own materials or tint.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabHighlight : MonoBehaviour
    {
        [SerializeField] Color hoverColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] Color heldColor = new Color(1f, 1f, 1f);
        [Tooltip("How much bigger the outline shell is than the item (0.06 = 6%).")]
        [SerializeField] float outlineScale = 0.06f;
        [SerializeField] float pulseSpeed = 4f;

        static Material s_SharedOutline;

        XRGrabInteractable m_Grab;
        Transform m_HaloRoot;
        readonly List<(Transform source, Transform halo)> m_Pairs = new List<(Transform, Transform)>();
        readonly List<Renderer> m_HaloRenderers = new List<Renderer>();
        MaterialPropertyBlock m_Block;
        int m_HoverCount;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Block = new MaterialPropertyBlock();
            m_Grab.hoverEntered.AddListener(OnHoverEntered);
            m_Grab.hoverExited.AddListener(OnHoverExited);
        }

        void OnDestroy()
        {
            if (m_Grab != null)
            {
                m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
                m_Grab.hoverExited.RemoveListener(OnHoverExited);
            }
            if (m_HaloRoot != null)
                Destroy(m_HaloRoot.gameObject);
        }

        void OnDisable()
        {
            m_HoverCount = 0;
            SetVisible(false);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            m_HoverCount++;
            SetVisible(true);
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            m_HoverCount = Mathf.Max(0, m_HoverCount - 1);
            if (m_HoverCount == 0 && !m_Grab.isSelected)
                SetVisible(false);
        }

        void LateUpdate()
        {
            if (m_HaloRoot == null || !m_HaloRoot.gameObject.activeSelf)
                return;

            // Hide again if the item stopped being grabbable mid-hover (locked, build phase over).
            if (!m_Grab.isActiveAndEnabled || (m_HoverCount == 0 && !m_Grab.isSelected))
            {
                SetVisible(false);
                return;
            }

            foreach (var (source, halo) in m_Pairs)
            {
                if (source == null)
                    continue;
                halo.SetPositionAndRotation(source.position, source.rotation);
                halo.localScale = source.lossyScale * (1f + outlineScale);
            }

            var held = m_Grab.isSelected;
            var glow = held ? 1.6f : 0.9f + 0.4f * Mathf.Sin(Time.time * pulseSpeed);
            var color = (held ? heldColor : hoverColor) * glow;
            m_Block.SetColor("_BaseColor", held ? heldColor : hoverColor);
            m_Block.SetColor("_EmissionColor", color);
            foreach (var haloRenderer in m_HaloRenderers)
                haloRenderer.SetPropertyBlock(m_Block);
        }

        void SetVisible(bool visible)
        {
            if (visible && m_HaloRoot == null)
                BuildHalo();
            if (m_HaloRoot != null)
                m_HaloRoot.gameObject.SetActive(visible);
        }

        void BuildHalo()
        {
            m_HaloRoot = new GameObject(name + " (Grab Highlight)").transform;
            var material = OutlineMaterial();
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var halo = new GameObject(filter.name + " Outline");
                halo.transform.SetParent(m_HaloRoot, false);
                halo.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var haloRenderer = halo.AddComponent<MeshRenderer>();
                haloRenderer.sharedMaterial = material;
                haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
                haloRenderer.receiveShadows = false;
                m_Pairs.Add((filter.transform, halo.transform));
                m_HaloRenderers.Add(haloRenderer);
            }
        }

        // One shared copy of the item's own URP Lit material, set to draw only back faces and glow.
        Material OutlineMaterial()
        {
            if (s_SharedOutline != null)
                return s_SharedOutline;

            var source = GetComponentInChildren<Renderer>();
            var template = source != null ? source.sharedMaterial : null;
            s_SharedOutline = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            s_SharedOutline.name = "Grab Highlight Outline";
            s_SharedOutline.SetTexture("_BaseMap", null);
            s_SharedOutline.SetFloat("_Cull", (float)CullMode.Front);
            s_SharedOutline.SetFloat("_Smoothness", 0f);
            s_SharedOutline.SetFloat("_Metallic", 0f);
            s_SharedOutline.EnableKeyword("_EMISSION");
            s_SharedOutline.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return s_SharedOutline;
        }
    }
}
