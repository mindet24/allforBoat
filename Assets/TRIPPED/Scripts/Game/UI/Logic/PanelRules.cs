using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>The third-party credits file: one credit per line, blank lines and # comments skipped.</summary>
    public static class CreditsRules
    {
        /// <summary>The credit lines of <paramref name="text"/>, trimmed, in file order.</summary>
        public static List<string> Parse(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                lines.Add(line);
            }
            return lines;
        }
    }

    /// <summary>One room's numbers for the power panel.</summary>
    public struct RoomPower
    {
        public string Id;
        public int Load;
        public int Capacity;
        public bool On;
        public bool Overloading;
    }

    /// <summary>Pure rules of the HUD power panel ("Total Power used/total" and the room list).</summary>
    public static class PowerPanelRules
    {
        /// <summary>
        /// House total: load summed over rooms whose breaker is ON, against the sum of every room's capacity
        /// (a dark room adds nothing to the load but its capacity still counts).
        /// </summary>
        public static Vector2Int Total(IReadOnlyList<RoomPower> rooms)
        {
            int used = 0, cap = 0;
            foreach (var r in rooms)
            {
                cap += Mathf.Max(0, r.Capacity);
                if (r.On) used += Mathf.Max(0, r.Load);
            }
            return new Vector2Int(used, cap);
        }

        /// <summary>
        /// Which rooms to list: all of them in the overview (or when not following); while following only the
        /// current room, plus any room that is overloading (so a trip about to happen elsewhere is never hidden).
        /// </summary>
        public static List<string> RowsToShow(IReadOnlyList<RoomPower> rooms, bool following, string currentRoom)
        {
            var ids = new List<string>();
            foreach (var r in rooms)
                if (!following || r.Id == currentRoom || r.Overloading) ids.Add(r.Id);
            return ids;
        }

        /// <summary>True when any room is over capacity right now (the panel shows its Overload! badge).</summary>
        public static bool AnyOverload(IReadOnlyList<RoomPower> rooms)
        {
            foreach (var r in rooms)
                if (r.On && r.Overloading) return true;
            return false;
        }

        /// <summary>"mm:ss" for a whole number of seconds (never negative).</summary>
        public static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }
}
