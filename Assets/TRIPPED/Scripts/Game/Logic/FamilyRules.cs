using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure rules for where the family stands in Day mode: a person waits in the room of their most pressing request
    /// (the level's PersonData: "they wait in the room until the request is done or missed"), otherwise in their first
    /// room. Several people in one room share it out evenly.
    /// </summary>
    public static class FamilyRules
    {
        /// <summary>
        /// Room id <paramref name="person"/> stands in: the room of their active request that is due first, or
        /// <paramref name="homeRooms"/>[0] when they want nothing. Null if they have neither.
        /// </summary>
        public static string RoomOf(string person, IReadOnlyList<string> homeRooms, IReadOnlyList<RequestState> requests)
        {
            RequestState best = null;
            if (requests != null)
                for (int i = 0; i < requests.Count; i++)
                {
                    var r = requests[i];
                    if (!r.IsActive || !string.Equals(r.Owner, person, StringComparison.OrdinalIgnoreCase)) continue;
                    if (best == null || r.DueTicks < best.DueTicks) best = r;
                }
            if (best != null) return best.RoomId;
            return homeRooms != null && homeRooms.Count > 0 ? homeRooms[0] : null;
        }

        /// <summary>True while <paramref name="person"/> has a request still waiting (not done, not missed).</summary>
        public static bool IsWaiting(string person, IReadOnlyList<RequestState> requests)
        {
            if (requests == null) return false;
            for (int i = 0; i < requests.Count; i++)
                if (requests[i].IsActive && string.Equals(requests[i].Owner, person, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Moves a person standing at <paramref name="x"/> (<paramref name="halfWidth"/> to each side) off the
        /// <paramref name="blocked"/> x spans (appliances drawn in front of them), to whichever free side is nearer,
        /// staying between <paramref name="minX"/> and <paramref name="maxX"/>. Unchanged if there is no free spot.
        /// </summary>
        public static float AvoidX(float x, float halfWidth, float minX, float maxX, IReadOnlyList<Vector2> blocked)
        {
            if (blocked == null || blocked.Count == 0) return x;
            float best = x;
            float bestMove = float.MaxValue;
            // Candidates: where they stand now, and just beside each blocked span.
            for (int c = -1; c < blocked.Count * 2; c++)
            {
                float cand = c < 0 ? x : (c % 2 == 0 ? blocked[c / 2].x - halfWidth : blocked[c / 2].y + halfWidth);
                if (cand - halfWidth < minX || cand + halfWidth > maxX) continue;
                bool free = true;
                for (int i = 0; i < blocked.Count && free; i++)
                    free = cand + halfWidth <= blocked[i].x + 0.0001f || cand - halfWidth >= blocked[i].y - 0.0001f;
                if (!free) continue;
                float move = Mathf.Abs(cand - x);
                if (move < bestMove) { bestMove = move; best = cand; }
            }
            return best;
        }

        /// <summary>
        /// World x of the <paramref name="slot"/>-th of <paramref name="count"/> people standing in a room from
        /// <paramref name="roomX"/> that is <paramref name="width"/> wide: evenly spaced, never at the walls.
        /// </summary>
        public static float SlotX(float roomX, float width, int slot, int count)
        {
            if (count <= 0) return roomX + width * 0.5f;
            return roomX + width * (slot + 1f) / (count + 1f);
        }
    }
}
