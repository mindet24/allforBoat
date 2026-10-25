namespace Trouble.Sim
{
    /// <summary>
    /// Day mode: when a new request deliberately clashes with what a room already holds (roadmap M2). A clash makes
    /// the player choose (run things one after another, use another room, or let someone wait), so it must always
    /// have a way out: the appliance fits the room on its own, and there is time to wait for everything else in the
    /// room to finish and still make it.
    /// </summary>
    /// <example>
    /// <code>
    /// // Kitchen 11: 7 running, a 3 waiting to start. A kettle (6) due in 40 s clashes; the 3 has 12 s left to run.
    /// bool clash = ConflictRules.IsSolvableConflict(11, 7, 3, 6, blockingTicks: 120, travelTicks: 100, dueTicks: 400);
    /// </code>
    /// </example>
    public static class ConflictRules
    {
        /// <summary>
        /// True if a request for <paramref name="newLoad"/> in a room of <paramref name="capacity"/> clashes with the
        /// <paramref name="roomLoad"/> drawn there now plus the <paramref name="pendingLoad"/> of requests there not yet
        /// running, and is still doable: it fits alone, and <paramref name="blockingTicks"/> (the use left on every
        /// other request in the room) plus <paramref name="travelTicks"/> (to carry it there) fit before
        /// <paramref name="dueTicks"/> (ticks from now until it is due).
        /// </summary>
        public static bool IsSolvableConflict(int capacity, int roomLoad, int pendingLoad, int newLoad, int blockingTicks,
            int travelTicks, int dueTicks)
        {
            if (newLoad <= 0 || newLoad > capacity) return false;
            if (roomLoad + pendingLoad + newLoad <= capacity) return false;
            return blockingTicks + travelTicks <= dueTicks;
        }
    }
}
