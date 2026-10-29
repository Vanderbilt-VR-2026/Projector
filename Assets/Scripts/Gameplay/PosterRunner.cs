using UnityEngine;

namespace Projector.Gameplay
{
    // A runner on the landing room's framed poster: hops along its local x from the start line to the finish,
    // then reappears back at the start, forever. Decoration only.
    public class PosterRunner : MonoBehaviour
    {
        [SerializeField] float startX = -0.6f;
        [SerializeField] float finishX = 0.6f;
        [SerializeField] float speed = 0.18f;
        [SerializeField] float hopHeight = 0.03f;

        float groundY;
        float distance;

        void Awake()
        {
            groundY = transform.localPosition.y;
            distance = transform.localPosition.x - startX;
        }

        void Update()
        {
            distance = Mathf.Repeat(distance + speed * Time.deltaTime, finishX - startX);
            var position = transform.localPosition;
            position.x = startX + distance;
            position.y = groundY + hopHeight * Mathf.Abs(Mathf.Sin(distance * 20f));
            transform.localPosition = position;
        }
    }
}
