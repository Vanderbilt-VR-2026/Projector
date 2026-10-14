using UnityEngine;

namespace Projector.Gameplay
{
    // A projected bounce pad (trampoline): a runner that lands on top is launched upward, well past a normal
    // jump. The border flashes on each bounce.
    public class BouncePad : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Upward speed given to a runner landing on the pad (a normal jump is 8.5).")]
        [SerializeField] float launchSpeed = 15f;
        [SerializeField] Color flashColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] float flashSeconds = 0.25f;

        Renderer border;
        Color restColor;
        MaterialPropertyBlock block;
        float flashUntil;

        public float LaunchSpeed => launchSpeed;

        public void Setup(Renderer borderRenderer, Color shapeColor)
        {
            border = borderRenderer;
            restColor = shapeColor * 0.3f;
            block = new MaterialPropertyBlock();
        }

        public void Flash() => flashUntil = Time.time + flashSeconds;

        void Update()
        {
            if (border == null)
                return;
            var color = Time.time < flashUntil ? flashColor : restColor;
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            border.SetPropertyBlock(block);
        }
    }
}
