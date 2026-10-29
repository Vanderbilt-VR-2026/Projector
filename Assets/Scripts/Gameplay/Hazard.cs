using UnityEngine;

namespace Projector.Gameplay
{
    // A projected hazard (spike strip, buzzsaw): touching it sends the runner back to the start and counts
    // as a fall. It pulses red so players can read it as dangerous at a glance.
    public class Hazard : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Color pulseColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] float pulseSpeed = 5f;

        Renderer shape;
        Color baseColor;
        MaterialPropertyBlock block;

        public void Setup(Renderer shapeRenderer, Color shapeColor)
        {
            shape = shapeRenderer;
            baseColor = shapeColor;
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (shape == null)
                return;
            var t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            var color = Color.Lerp(baseColor, pulseColor, t);
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            shape.SetPropertyBlock(block);
        }
    }
}
