using UnityEngine;

namespace Projector.Gameplay
{
    // A projected bounce pad (trampoline): a runner that lands on top is launched upward, well past a normal
    // jump. It flashes on each bounce.
    public class BouncePad : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Upward speed given to a runner landing on the pad (a normal jump is 8.5).")]
        [SerializeField] float launchSpeed = 15f;
        [SerializeField] Color flashColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] float flashSeconds = 0.25f;

        Renderer shape;
        Color restColor;
        MaterialPropertyBlock block;
        float flashUntil;

        public float LaunchSpeed => launchSpeed;

        public void Setup(Renderer shapeRenderer, Color shapeColor)
        {
            shape = shapeRenderer;
            restColor = shapeColor;
            block = new MaterialPropertyBlock();
        }

        public void Flash() => flashUntil = Time.time + flashSeconds;

        void Update()
        {
            if (shape == null)
                return;
            var color = Time.time < flashUntil ? flashColor : restColor;
            color.a = 1f;
            block.SetColor(BaseColorId, color);
            shape.SetPropertyBlock(block);
        }
    }
}
