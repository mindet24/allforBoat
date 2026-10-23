using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>Colour band of a load bar (GDD v1.0 §4.2).</summary>
    public enum LoadBand { Green, Amber, Red }

    /// <summary>
    /// Pure text and number rules for the HUD: clocks, load bands, countdowns, queue labels.
    /// No UnityEngine state, so it is covered by EditMode tests.
    /// </summary>
    public static class HudFormat
    {
        /// <summary>Ticks as m:ss, rounded down to the second (same rule as the cause sentences).</summary>
        public static string Clock(int ticks, int ticksPerSecond)
        {
            return CauseFormatter.Clock(ticks < 0 ? 0 : ticks, ticksPerSecond <= 0 ? 1 : ticksPerSecond);
        }

        /// <summary>The top-bar clock: "0:12 / 0:35".</summary>
        public static string ClockPair(int nowTicks, int dayTicks, int ticksPerSecond)
        {
            return Clock(nowTicks, ticksPerSecond) + " / " + Clock(dayTicks, ticksPerSecond);
        }

        /// <summary>
        /// Time left until a deadline as m:ss, rounded up so it reads 0:01 until the deadline tick itself.
        /// Never negative.
        /// </summary>
        public static string TimeLeft(int dueTicks, int nowTicks, int ticksPerSecond)
        {
            if (ticksPerSecond <= 0) ticksPerSecond = 1;
            int left = dueTicks - nowTicks;
            if (left < 0) left = 0;
            int seconds = (left + ticksPerSecond - 1) / ticksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        /// <summary>
        /// Band for a load bar: at or below <paramref name="amberAbove"/> of capacity is green, at or below
        /// <paramref name="redAbove"/> is amber, above is red. A capacity of 0 is red as soon as there is any load.
        /// </summary>
        public static LoadBand Band(int load, int capacity, float amberAbove = 0.7f, float redAbove = 1f)
        {
            if (capacity <= 0) return load > 0 ? LoadBand.Red : LoadBand.Green;
            float ratio = (float)load / capacity;
            if (ratio <= amberAbove) return LoadBand.Green;
            if (ratio <= redAbove) return LoadBand.Amber;
            return LoadBand.Red;
        }

        /// <summary>How full a load bar is drawn, 0..1 (a bar over capacity is drawn full).</summary>
        public static float Fill(int load, int capacity)
        {
            if (capacity <= 0) return load > 0 ? 1f : 0f;
            float f = (float)load / capacity;
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }

        /// <summary>"6 / 8".</summary>
        public static string LoadText(int load, int capacity) => load + " / " + capacity;

        /// <summary>Seconds until an overloaded breaker trips, given its counter (R3). Never negative.</summary>
        public static float SecondsToTrip(int overTicks, int overTripTicks, int ticksPerSecond)
        {
            if (ticksPerSecond <= 0) ticksPerSecond = 1;
            int left = overTripTicks - overTicks;
            return left <= 0 ? 0f : (float)left / ticksPerSecond;
        }

        /// <summary>Request progress 0..1.</summary>
        public static float Progress(int progressTicks, int durationTicks)
        {
            if (durationTicks <= 0) return 1f;
            float f = (float)progressTicks / durationTicks;
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }

        /// <summary>Short label for an action in the character's queue, e.g. "Plug Kettle", "Breaker A".</summary>
        /// <param name="nameOf">Turns an appliance id into its display name.</param>
        /// <param name="roomName">Turns a room id into its display name.</param>
        public static string ActionLabel(Command c, System.Func<string, string> nameOf, System.Func<string, string> roomName)
        {
            switch (c.Type)
            {
                case CommandType.Plug: return "Plug " + nameOf(c.A);
                case CommandType.Unplug: return "Unplug " + nameOf(c.A);
                case CommandType.SetSwitch: return (c.On ? "On " : "Off ") + nameOf(c.A);
                case CommandType.ToggleBreaker: return "Breaker " + c.A;
                case CommandType.PlaceCord: return "Cord " + c.A;
                case CommandType.RemoveCord: return "Remove cord";
                case CommandType.ToggleDoor: return "Door " + c.A;
                case CommandType.MoveTo: return "Go to " + roomName(c.A);
                default: return c.Type.ToString();
            }
        }
    }
}
