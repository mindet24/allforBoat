using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>How urgent a day request is, for colour and wording.</summary>
    public enum UrgencyLevel { Normal, Urgent, Relaxed }

    /// <summary>
    /// Pure rules for the Day-mode view (level_demo): blackout state, request card fractions and texts. They only read
    /// sim state the player is meant to see.
    /// </summary>
    public static class DayRules
    {
        /// <summary>"urgent", "normal", "relaxed" (any case); unknown ids count as normal.</summary>
        public static UrgencyLevel ParseUrgency(string id)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "urgent": return UrgencyLevel.Urgent;
                case "relaxed": return UrgencyLevel.Relaxed;
                default: return UrgencyLevel.Normal;
            }
        }

        /// <summary>A blackout: day mode, the level still going, and at least one room's breaker is OFF.</summary>
        public static bool IsBlackout(HouseSim sim)
        {
            if (sim == null || !sim.IsDayMode) return false;
            if (sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost) return false;
            foreach (var c in sim.Circuits)
                if (!c.IsOn) return true;
            return false;
        }

        /// <summary>Request is running (some progress) or already finished: it can no longer be missed.</summary>
        public static bool HasStarted(RequestState r) => r.Done || r.ProgressTicks > 0;

        /// <summary>
        /// How much of the waiting time is left before the request must start, 1 → 0, from its spawn to its due tick.
        /// </summary>
        public static float WaitFraction(int spawnTick, int dueTick, int now)
        {
            int span = dueTick - spawnTick;
            if (span <= 0) return 0f;
            return Mathf.Clamp01((float)(dueTick - now) / span);
        }

        /// <summary>Time on the day clock still to go, never negative.</summary>
        public static int TicksLeft(int dayLengthTicks, int now) => Mathf.Max(0, dayLengthTicks - now);

        /// <summary>"Kitchen 6/8".</summary>
        public static string RoomLoadText(string roomName, int load, int capacity) => roomName + " " + load + "/" + capacity;

        /// <summary>Hearts to draw filled, clamped to 0..max.</summary>
        public static int FilledHearts(int hearts, int max) => Mathf.Clamp(hearts, 0, Mathf.Max(0, max));

        /// <summary>
        /// Active (not done, not missed) requests count, for the card stack and the room markers.
        /// </summary>
        public static int ActiveCount(HouseSim sim)
        {
            int n = 0;
            foreach (var r in sim.Requests)
                if (r.IsActive) n++;
            return n;
        }
    }
}
