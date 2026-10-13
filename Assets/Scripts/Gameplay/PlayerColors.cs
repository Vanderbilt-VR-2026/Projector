using UnityEngine;

namespace Projector.Gameplay
{
    // One color per player slot (SessionService.MaxPlayers): avatars, runners, lobby pads and scoreboard rows.
    public static class PlayerColors
    {
        public static readonly Color[] All =
        {
            new Color(0.86f, 0.24f, 0.22f),
            new Color(0.22f, 0.45f, 0.88f),
            new Color(0.25f, 0.72f, 0.33f),
            new Color(0.95f, 0.78f, 0.2f)
        };

        public static Color Get(int slot) => All[Mathf.Abs(slot) % All.Length];
    }
}
