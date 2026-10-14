using UnityEngine;

namespace Projector.Gameplay
{
    // A projected hazard (spike strip, buzzsaw): touching it sends the runner back to the start and counts
    // as a fall. Its border pulses red so players can read it as dangerous at a glance.
    public class Hazard : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Color pulseColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] float pulseSpeed = 5f;

        Renderer border;
        Color baseColor;
        MaterialPropertyBlock block;

        public void Setup(Renderer borderRenderer, Color shapeColor)
        {
            border = borderRenderer;
            baseColor = shapeColor * 0.3f;
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (border == null)
                return;
            var t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            var color = Color.Lerp(baseColor, pulseColor, t);
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            border.SetPropertyBlock(block);
        }
    }
}
