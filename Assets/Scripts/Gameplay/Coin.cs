using UnityEngine;

namespace Projector.Gameplay
{
    // A local pickup: each player collects their own copy, and the race manager counts it for them.
    public class Coin : MonoBehaviour
    {
        void Update()
        {
            transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        }

        public void Collect()
        {
            gameObject.SetActive(false);
        }
    }
}
