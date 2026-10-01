using System.Collections.Generic;
using Unity.Netcode;

namespace Projector.Networking
{
    // Server-side: gives each connected client a stable slot (0..MaxPlayers-1) for the whole game,
    // so a player keeps the same color and spawn point from the lobby to the scoreboard.
    public static class PlayerRoster
    {
        static readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
        static NetworkManager subscribedManager;

        public static int SlotFor(ulong clientId)
        {
            Subscribe();
            if (slots.TryGetValue(clientId, out var slot))
                return slot;

            slot = 0;
            while (slots.ContainsValue(slot))
                slot++;
            slots[clientId] = slot;
            return slot;
        }

        static void Subscribe()
        {
            var manager = NetworkManager.Singleton;
            if (manager == subscribedManager)
                return;

            slots.Clear();
            subscribedManager = manager;
            manager.OnClientDisconnectCallback += clientId => slots.Remove(clientId);
            manager.OnServerStopped += _ => slots.Clear();
        }
    }
}
