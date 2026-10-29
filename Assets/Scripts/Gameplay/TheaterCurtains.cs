using UnityEngine;

namespace Projector.Gameplay
{
    // The theater's two curtains (built by QuestRoomSceneBuilder, gathered and tied back either side of the
    // screen). When a player is taken back to the lobby the tie ropes drop to the floor and the curtains slide
    // shut across the screen. The curtain mesh's uv.x is how far across the curtain a vertex is (0 at its outer
    // edge), so closing just spreads every row out to the full closed width.
    public class TheaterCurtains : MonoBehaviour
    {
        [SerializeField] MeshFilter[] drapes;
        [Tooltip("The gold ties holding the curtains back; they fall when the curtains close.")]
        [SerializeField] Transform[] ties;
        [SerializeField] float closeSeconds = 1.6f;
        [Tooltip("How far past the middle of the screen each curtain reaches when closed.")]
        [SerializeField] float overlap = 0.1f;

        public static TheaterCurtains Current { get; private set; }

        public bool Closing { get; private set; }

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public async Awaitable CloseAsync()
        {
            if (Closing || drapes == null || drapes.Length == 0)
                return;
            Closing = true;

            var open = drapes[0].sharedMesh;
            var mesh = Instantiate(open);
            foreach (var drape in drapes)
                drape.sharedMesh = mesh;
            var from = open.vertices;
            var uvs = open.uv;
            var to = new Vector3[from.Length];
            var closedWidth = Vector3.Distance(drapes[0].transform.position, drapes[drapes.Length - 1].transform.position) / 2f + overlap;
            for (var i = 0; i < from.Length; i++)
                to[i] = new Vector3(uvs[i].x * closedWidth, from[i].y, from[i].z);

            var tieStarts = new Vector3[ties.Length];
            for (var i = 0; i < ties.Length; i++)
                tieStarts[i] = ties[i].position;

            var current = new Vector3[from.Length];
            for (var elapsed = 0f; elapsed < closeSeconds; elapsed += Time.deltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / closeSeconds);
                for (var i = 0; i < current.Length; i++)
                    current[i] = Vector3.LerpUnclamped(from[i], to[i], t);
                mesh.vertices = current;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                DropTies(tieStarts, elapsed);
                await Awaitable.NextFrameAsync();
                if (this == null)
                    return;
            }
            mesh.vertices = to;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // The ties fall freely to the floor, tipping over as they go.
        void DropTies(Vector3[] starts, float elapsed)
        {
            for (var i = 0; i < ties.Length; i++)
            {
                var fall = Mathf.Min(0.5f * 9.81f * elapsed * elapsed, starts[i].y - 0.03f);
                ties[i].position = starts[i] + Vector3.down * fall;
                ties[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Min(fall * 25f, 80f) * (i % 2 == 0 ? 1f : -1f));
            }
        }
    }
}
